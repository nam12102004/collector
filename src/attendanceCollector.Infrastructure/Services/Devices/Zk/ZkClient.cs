using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace attendanceCollector.Infrastructure.Services.Devices.Zk;
internal sealed class AttendanceRecord
{
    public AttendanceRecord(string userId, DateTime timestamp, int status, int punch, long uid)
    {
        UserId = userId;
        Timestamp = timestamp;
        Status = status;
        Punch = punch;
        Uid = uid;
    }

    public string UserId { get; }
    public DateTime Timestamp { get; }
    public int Status { get; }
    public int Punch { get; }
    public long Uid { get; }
}

internal sealed class PacketHeader
{
    public PacketHeader(ushort command, ushort sessionId, ushort replyId, byte[] payload)
    {
        Command = command;
        SessionId = sessionId;
        ReplyId = replyId;
        Payload = payload;
    }

    public ushort Command { get; }
    public ushort SessionId { get; }
    public ushort ReplyId { get; }
    public byte[] Payload { get; }
}

internal sealed class ZkClient : IDisposable
{
    private const ushort MachineHeader1 = 20560;
    private const ushort MachineHeader2 = 32130;
    private const ushort UShortMax = 65535;
    private const ushort CmdGetFreeSizes = 50;
    private const ushort CmdOptionsWrite = 12;
    private const ushort CmdConnect = 1000;
    private const ushort CmdExit = 1001;
    private const ushort CmdAuth = 1102;
    private const ushort CmdGetVersion = 1100;
    private const ushort CmdReadWithBuffer = 1503;
    private const ushort CmdReadChunk = 1504;
    private const ushort CmdPrepareData = 1500;
    private const ushort CmdData = 1501;
    private const ushort CmdFreeData = 1502;
    private const ushort CmdAttLogRrq = 13;

    private const ushort AckOk = 2000;
    private const ushort AckError = 2001;
    private const ushort AckUnauthorized = 2005;

    private const int MaximumPayloadBytes = 64 * 1024 * 1024;

    private readonly string _ip;
    private readonly int _port;
    private readonly int _password;
    private readonly int _timeoutSeconds;
    private readonly int _maximumRecords;
    private readonly bool _debug;
    private TcpClient _client = new();
    private NetworkStream _stream = null!;
    private ushort _sessionId;
    private ushort _replyId = UShortMax - 1;

    private int _records;

    public ZkClient(string ip, int port, int password, int timeoutSeconds, int maximumRecords = 200_000, bool debug = false)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            throw new ArgumentException("IP không được để trống", nameof(ip));
        }

        _ip = ip;
        _port = port;
        _password = password;
        _timeoutSeconds = timeoutSeconds;
        _maximumRecords = maximumRecords;
        _debug = debug;
    }

    public void Connect()
    {
        _client = new TcpClient();
        _client.ReceiveTimeout = _timeoutSeconds * 1000;
        _client.SendTimeout = _timeoutSeconds * 1000;
        var connectTask = _client.ConnectAsync(_ip, _port);
        if (!connectTask.Wait(_timeoutSeconds * 1000))
        {
            throw new Exception("Kết nối timeout");
        }

        if (!_client.Connected)
        {
            throw new Exception($"Không kết nối được tới {_ip}:{_port}");
        }

        _stream = _client.GetStream();

        _sessionId = 0;
        _replyId = UShortMax - 1;
        var connectResponse = SendCommand(CmdConnect, Array.Empty<byte>());
        _sessionId = connectResponse.SessionId;
        if (_debug)
        {
            Console.WriteLine($"CONNECT resp cmd=0x{connectResponse.Command:X4}, session={connectResponse.SessionId}, reply={connectResponse.ReplyId}");
        }
        if (connectResponse.Command == AckUnauthorized)
        {
            var authKey = BuildCommKey(_password, _sessionId);
            var authResponse = SendCommand(CmdAuth, authKey);
            if (_debug)
            {
                Console.WriteLine($"AUTH resp cmd=0x{authResponse.Command:X4}, session={authResponse.SessionId}, reply={authResponse.ReplyId}");
            }
            if (authResponse.Command != AckOk)
            {
                throw new Exception("Xác thực thiết bị thất bại");
            }
        }
        else if (connectResponse.Command != AckOk)
        {
            throw new Exception("Không thể kết nối thiết bị");
        }
    }

    public void Disconnect()
    {
        if (_client == null || !_client.Connected)
        {
            return;
        }

        try
        {
            SendCommand(CmdExit, Array.Empty<byte>());
        }
        catch
        {
            // ignore
        }
        _stream.Close();
        _client.Close();
    }

    public List<AttendanceRecord> GetAttendance()
    {
        ReadSizes();
        if (_records < 0 || _records > _maximumRecords)
        {
            throw new InvalidOperationException($"The device record count exceeds the configured limit of {_maximumRecords}.");
        }
        if (_records == 0)
        {
            return new List<AttendanceRecord>();
        }

        var (rawData, dataSize) = ReadWithBuffer(CmdAttLogRrq, 0, 0);
        if (dataSize <= 4)
        {
            return new List<AttendanceRecord>();
        }

        var totalSize = BinaryPrimitives.ReadInt32LittleEndian(rawData.AsSpan(0, 4));
        if (totalSize <= 0 || totalSize > MaximumPayloadBytes || totalSize % _records != 0)
        {
            throw new InvalidDataException("Device attendance payload size is invalid.");
        }

        var body = rawData.AsSpan(4, dataSize - 4).ToArray();
        var recordSize = totalSize / Math.Max(_records, 1);
        var records = new List<AttendanceRecord>();

        if (recordSize == 8)
        {
            for (int i = 0; i + 8 <= body.Length; i += 8)
            {
                var uid = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(i, 2));
                var status = body[i + 2];
                var timestamp = DecodeTime(BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(i + 3, 4)));
                var punch = body[i + 7];
                records.Add(new AttendanceRecord("", timestamp, status, punch, uid));
            }
        }
        else if (recordSize == 16)
        {
            for (int i = 0; i + 16 <= body.Length; i += 16)
            {
                var userIdNumber = BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(i, 4));
                var userId = userIdNumber.ToString(CultureInfo.InvariantCulture);
                var timestamp = DecodeTime(BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(i + 4, 4)));
                var status = body[i + 8];
                var punch = body[i + 9];
                var uid = (long)userIdNumber;
                records.Add(new AttendanceRecord(userId, timestamp, status, punch, uid));
            }
        }
        else if (recordSize >= 40)
        {
            for (int i = 0; i + 40 <= body.Length; i += recordSize)
            {
                var uid = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(i, 2));
                var userId = ReadCString(body.AsSpan(i + 2, 24), Encoding.UTF8);
                var status = body[i + 26];
                var timestamp = DecodeTime(BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(i + 27, 4)));
                var punch = body[i + 31];
                records.Add(new AttendanceRecord(userId, timestamp, status, punch, uid));
            }
        }

        else
        {
            throw new NotSupportedException($"Unsupported attendance record size: {recordSize} bytes.");
        }

        if (records.Count != _records)
            throw new InvalidDataException("Device returned an incomplete attendance record set.");

        return records;
    }

    private void ReadSizes()
    {
        var response = SendCommand(CmdGetFreeSizes, Array.Empty<byte>());
        if (response.Command != AckOk)
        {
            throw new Exception("Không đọc được thông tin kích thước máy");
        }

        var data = response.Payload;
        if (data.Length < 36)
            throw new InvalidDataException("The device did not return a valid record count.");
        _records = BitConverter.ToInt32(data, 32);
    }

    private (byte[] Data, int Size) ReadWithBuffer(ushort command, int functionCode, int extCode)
    {
        byte[] request = new byte[11];
        request[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(request.AsSpan(1), (short)command);
        BinaryPrimitives.WriteInt32LittleEndian(request.AsSpan(3), functionCode);
        BinaryPrimitives.WriteInt32LittleEndian(request.AsSpan(7), extCode);

        var response = SendCommand(CmdReadWithBuffer, request);
        if (_debug)
        {
            Console.WriteLine($"READ_BUFFER 0x{command:X4} -> first resp=0x{response.Command:X4}, payload={response.Payload.Length}");
        }
        if (response.Command == CmdData)
        {
            if (response.Payload.Length >= 4)
            {
                var expectedDataSize = BinaryPrimitives.ReadInt32LittleEndian(response.Payload.AsSpan(0, 4));
                if (expectedDataSize < 0 || expectedDataSize > MaximumPayloadBytes)
                    throw new InvalidDataException("Device attendance payload size is invalid.");
                if (expectedDataSize > response.Payload.Length - 4)
                {
                    var additional = expectedDataSize - (response.Payload.Length - 4);
                    var extraData = ReadExact(additional);
                    var data = new byte[response.Payload.Length + additional];
                    Buffer.BlockCopy(response.Payload, 0, data, 0, response.Payload.Length);
                    Buffer.BlockCopy(extraData, 0, data, response.Payload.Length, extraData.Length);
                    return (data, data.Length);
                }
            }
            return (response.Payload, response.Payload.Length);
        }

        if (response.Command != CmdPrepareData && response.Command != AckOk)
        {
            throw new Exception($"Lệnh 0x{command:X4} không trả về dữ liệu đúng (code: {response.Command})");
        }

        if (response.Payload.Length < 5)
        {
            throw new Exception("Phản hồi CMD_PREPARE_DATA không hợp lệ");
        }

        var expectedSize = BinaryPrimitives.ReadInt32LittleEndian(response.Payload.AsSpan(1, 4));
        if (expectedSize < 0 || expectedSize > MaximumPayloadBytes)
            throw new InvalidDataException("Device attendance payload size is invalid.");
        using var buffer = new MemoryStream();

        var chunkSize = 0xFFc0;
        var chunkCount = expectedSize / chunkSize;
        var chunkRemain = expectedSize % chunkSize;
        var start = 0;
        for (var i = 0; i < chunkCount; i++)
        {
            var data = ReadChunk(start, chunkSize);
            buffer.Write(data, 0, data.Length);
            start += chunkSize;
        }

        if (chunkRemain > 0)
        {
            var data = ReadChunk(start, chunkRemain);
            buffer.Write(data, 0, data.Length);
        }

        SendCommand(CmdFreeData, Array.Empty<byte>());

        if (buffer.Length < expectedSize)
        {
            throw new Exception($"Dữ liệu trả về thiếu: kỳ vọng {expectedSize}, nhận {buffer.Length}");
        }

        return (buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, expectedSize)).ToArray(), expectedSize);
    }

    private byte[] ReadChunk(int start, int size)
    {
        var request = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(request.AsSpan(0), start);
        BinaryPrimitives.WriteInt32LittleEndian(request.AsSpan(4), size);

        var response = SendCommand(CmdReadChunk, request);
        if (_debug)
        {
            Console.WriteLine($"READ_CHUNK start={start} size={size} -> cmd=0x{response.Command:X4}, payload={response.Payload.Length}");
        }

        if (response.Command == CmdPrepareData)
        {
            if (response.Payload.Length < 8)
            {
                throw new Exception("Phản hồi chunk không hợp lệ");
            }

            var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(response.Payload.AsSpan(0, 4));
            using var buffer = new MemoryStream();
            var initialChunk = response.Payload.Length - 8;
            if (initialChunk > 0)
            {
                buffer.Write(response.Payload, 8, initialChunk);
            }

            var remaining = chunkSize - initialChunk;
            if (remaining > 0)
            {
                var chunkPacket = ReadPacket();
                if (chunkPacket.Command != CmdData)
                {
                    throw new Exception($"Không đọc được dữ liệu chunk, lỗi code: {chunkPacket.Command}");
                }

                buffer.Write(chunkPacket.Payload);
                while (buffer.Length < chunkSize)
                {
                    var nextChunk = ReadPacket();
                    if (nextChunk.Command == CmdData)
                    {
                        buffer.Write(nextChunk.Payload);
                    }
                    else if (nextChunk.Command == AckOk)
                    {
                        break;
                    }
                    else
                    {
                        throw new Exception($"Không đọc được dữ liệu chunk, lỗi code: {nextChunk.Command}");
                    }
                }
            }

            var ackPacket = ReadPacket();
            if (ackPacket.Command != AckOk)
            {
                throw new Exception($"Không đọc được ACK sau chunk, lỗi code: {ackPacket.Command}");
            }

            return buffer.ToArray();
        }

        if (response.Command != CmdData)
        {
            throw new Exception($"Không đọc được dữ liệu chunk, lỗi code: {response.Command}");
        }

        if (response.Payload.Length == size)
        {
            return response.Payload;
        }

        if (response.Payload.Length < size)
        {
            var additional = size - response.Payload.Length;
            var extra = ReadExact(additional);
            var data = new byte[size];
            Buffer.BlockCopy(response.Payload, 0, data, 0, response.Payload.Length);
            Buffer.BlockCopy(extra, 0, data, response.Payload.Length, extra.Length);
            return data;
        }

        return response.Payload;
    }

    private PacketHeader SendCommand(ushort command, byte[] payload)
    {
        var header = BuildHeader(command, payload);
        var bodyLength = (ushort)header.Length;
        byte[] top = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(top.AsSpan(0), MachineHeader1);
        BinaryPrimitives.WriteUInt16LittleEndian(top.AsSpan(2), MachineHeader2);
        BinaryPrimitives.WriteUInt32LittleEndian(top.AsSpan(4), bodyLength);
        _stream.Write(top);
        _stream.Write(header);
        _stream.Flush();

        var packet = ReadPacket();
        if (_debug)
        {
            Console.WriteLine($"TX 0x{command:X4} -> RX 0x{packet.Command:X4} sid={packet.SessionId} reply={packet.ReplyId} payload={packet.Payload.Length}");
        }
        return new PacketHeader(packet.Command, packet.SessionId, packet.ReplyId, packet.Payload);
    }

    private PacketHeader ReadPacket()
    {
        var top = ReadExact(8);
        var machineValue1 = BinaryPrimitives.ReadUInt16LittleEndian(top.AsSpan(0, 2));
        var machineValue2 = BinaryPrimitives.ReadUInt16LittleEndian(top.AsSpan(2, 2));
        var bodyLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(top.AsSpan(4, 4));

        if (machineValue1 != MachineHeader1 || machineValue2 != MachineHeader2)
        {
            throw new Exception("Gói tin phản hồi không hợp lệ");
        }

        if (bodyLength < 8)
        {
            throw new Exception("Độ dài gói tin không hợp lệ");
        }

        var body = ReadExact(bodyLength);
        var command = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(0, 2));
        var sessionId = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(4, 2));
        var replyId = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(6, 2));
        var payload = body.AsSpan(8).ToArray();
        return new PacketHeader(command, sessionId, replyId, payload);
    }

    private byte[] ReadExact(int length)
    {
        var result = new byte[length];
        int offset = 0;
        while (offset < length)
        {
            var read = _stream.Read(result, offset, length - offset);
            if (read <= 0)
            {
                throw new Exception("Mất kết nối khi đọc dữ liệu");
            }

            offset += read;
        }

        return result;
    }

    private static string ReadCString(ReadOnlySpan<byte> bytes, Encoding encoding)
    {
        var end = 0;
        while (end < bytes.Length && bytes[end] != 0)
        {
            end++;
        }

        return encoding.GetString(bytes.Slice(0, end));
    }

    private static DateTime DecodeTime(uint encoded)
    {
        var second = (int)(encoded % 60);
        encoded /= 60;
        var minute = (int)(encoded % 60);
        encoded /= 60;
        var hour = (int)(encoded % 24);
        encoded /= 24;
        var day = (int)(encoded % 31) + 1;
        encoded /= 31;
        var month = (int)(encoded % 12) + 1;
        var year = (int)(encoded / 12) + 2000;
        return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
    }

    private static byte[] BuildCommKey(int password, int sessionId, int ticks = 50)
    {
        uint key = (uint)password;
        uint session = (uint)sessionId;
        uint computed = 0;
        for (int i = 0; i < 32; i++)
        {
            if (((key >> i) & 1) == 1)
            {
                computed = (computed << 1) | 1;
            }
            else
            {
                computed <<= 1;
            }
        }

        computed += session;
        var packed = BitConverter.GetBytes(computed);
        packed[0] ^= (byte)'Z';
        packed[1] ^= (byte)'K';
        packed[2] ^= (byte)'S';
        packed[3] ^= (byte)'O';
        var firstWord = BitConverter.ToUInt16(packed, 0);
        var secondWord = BitConverter.ToUInt16(packed, 2);
        var combined = new byte[4];
        BitConverter.GetBytes(secondWord).CopyTo(combined.AsSpan(0, 2));
        BitConverter.GetBytes(firstWord).CopyTo(combined.AsSpan(2, 2));
        var salt = (byte)(ticks & 0xFF);
        combined[0] ^= salt;
        combined[1] ^= salt;
        combined[2] = salt;
        combined[3] ^= salt;
        return combined;
    }

    private static ushort Checksum(ReadOnlySpan<byte> data)
    {
        int checksum = 0;
        int count = data.Length;
        int index = 0;
        while (count > 1)
        {
            checksum += data[index] | (data[index + 1] << 8);
            index += 2;
            count -= 2;
            if (checksum > UShortMax)
            {
                checksum -= UShortMax;
            }
        }

        if (count == 1)
        {
            checksum += data[index];
        }

        while (checksum > UShortMax)
        {
            checksum -= UShortMax;
        }

        checksum = ~checksum;
        while (checksum < 0)
        {
            checksum += UShortMax;
        }

        return (ushort)(checksum & 0xFFFF);
    }

    private byte[] BuildHeader(ushort command, ReadOnlySpan<byte> payload)
    {
        var header = new byte[8 + payload.Length];
        var currentReplyId = _replyId;
        var nextReplyId = (ushort)(_replyId + 1);
        if (nextReplyId >= UShortMax)
        {
            nextReplyId -= UShortMax;
        }

        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0), command);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4), _sessionId);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(6), currentReplyId);
        payload.CopyTo(header.AsSpan(8));

        var checksum = Checksum(header);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(2), checksum);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(6), nextReplyId);

        _replyId = nextReplyId;

        return header;
    }

    public void Dispose()
    {
        Disconnect();
    }
}

