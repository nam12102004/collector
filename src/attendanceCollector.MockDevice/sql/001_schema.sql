CREATE TABLE IF NOT EXISTS mock_device_events (
    event_id varchar(128) PRIMARY KEY,
    employee_code varchar(100) NOT NULL,
    employee_name varchar(200) NOT NULL,
    device_local_time timestamp without time zone NOT NULL,
    status integer NOT NULL,
    punch integer NOT NULL,
    source_record_uid integer NULL,
    created_at_utc timestamp with time zone NOT NULL,
    sent_at_utc timestamp with time zone NULL
);

CREATE INDEX IF NOT EXISTS ix_mock_device_events_sent_created
    ON mock_device_events (sent_at_utc, created_at_utc);
