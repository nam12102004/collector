---
name: aubot-enum-contracts
description: Audit magic strings and refactor finite domain vocabularies into typed contracts in Aubot WCS, including .NET modules, shared events/read models, mocks, Angular clients, and persisted JSON/outbox compatibility.
---

# Aubot enum contracts

Apply these rules when auditing state/type/action strings or changing enum contracts in this repository. Paths below are relative to the repository root.

## Choose one vocabulary

- Use enums for finite domain vocabularies owned by the application: states, types, stop intent, rehandling roles, recovery actions and classifications.
- Use enum members directly. Remove constant classes or private constants that merely alias enum members, such as `const StepStopRequestEnum Cancel = StepStopRequestEnum.CANCEL`, and update their callers. Do not keep wrapper constants for source compatibility.
- Keep strings for identifiers, OPC node addresses, messages, error codes and metadata whose values are genuinely open ended. A field name ending in `State` or `Type` is not sufficient evidence to convert it.
- Retain useful collections and mappings, such as allowed-state sets, runtime validation catalogs and translated labels; these serve a purpose beyond aliasing one enum member.
- Put vocabularies shared across modules in `src/Aubot.Shared/Enums`. Keep a vocabulary local when only one module owns it. Do not introduce a reference to another module's domain assembly just to access an enum.

## Update the whole contract

Trace the affected value through entities, DTOs, interfaces, repositories, validation, EF configuration, event producers/consumers, persisted snapshots, outbox serialization, host read models, HTTP/SignalR, frontend models and reducers, mocks, fixtures and tests.

- Replace string comparisons, parsing and enum-to-string conversions inside typed application flows with enum operations. Convert to text where the destination actually requires text, such as a generic audit label or confirmation phrase.
- Preserve existing PLC and persisted numeric codes. Keep crane execution state, equipment health and operating mode distinct; a health update must not overwrite execution state or imply AUTO mode.
- Preserve command identity checks, ACK conditions and recovery release conditions during a type refactor.
- Validate command inputs for enum membership. If an enum includes `UNKNOWN` for read models or historical data, reject it at command entry points that require a concrete operation.
- Historical action types may remain readable without being allowed as new operations. Keep operation validation explicit.

## Persisted and wire compatibility

- Inspect actual producers, serializer options and representative stored values before deciding which historical forms need support. Do not invent legacy aliases or add arbitrary unknown values to the enum to make fixtures pass.
- Reuse `CompatibleStringEnumConverter<TEnum>`, `CompatibleStringEnumConverterFactory` and `LegacyEnumAliasAttribute` in `src/Aubot.Shared/Serialization` for relevant contracts. Put historical aliases at the deserialization boundary and emit the canonical wire name on write.
- Check default `JsonSerializer` calls used by stored JSON as well as configured HTTP, SignalR, MassTransit and mock serializers. A global converter can override an enum's converter attribute, so verify the effective options.
- Preserve meaningful wire names, including existing lowercase names declared with `EnumMember`. Accept defined historical numbers only when they represent the same enum values; reject undefined names and numbers.
- Preserve nullable stop intent and rehandling roles. Accept null/empty as a non-null default only where the previous contract actually defined that behavior.
- For existing text columns, retain string conversions such as `HasConversion<string>()` unless a storage change is part of the task. Compare the EF model with its migration snapshot before deciding whether a migration is needed. Do not reset stored data as a shortcut for a type refactor.
- Keep serialized event identities stable when only field types change, so existing outbox entries can resolve their message types. Follow any explicitly requested breaking-change or data-migration strategy instead of retaining redundant source aliases.

## Angular boundaries

- Follow the runtime catalogs and derived string unions in `src/Aubot.WCS.WebApp/src/app/features/omc/models/domain-state.model.ts`; maintain one vocabulary rather than duplicating lists in individual decoders or components.
- Validate unknown transport values with the shared readers. Do not assert arbitrary strings as a domain enum/union to silence type errors.
- Normalize supported historical aliases at snapshot and SignalR boundaries using `utils/realtime-enum-codec.ts`. Invalid values must not be inserted into typed state; preserve the reducer's existing handling of rejected or missing values.
- Keep action request types restricted to supported operations while allowing historical response types to represent retired actions.
- Keep mock API wire values aligned with their consumers, and update mock scenarios and UI fixtures when the contract changes.

## Verify the affected slice

Use the nearest focused tests for behavior changes. For a cross-module refactor, build the host and affected mocks, run the affected backend suites, and type-check/test the frontend when its contracts change.

Cover meaningful compatibility cases: historical names/casing, defined numeric values where applicable, nullable fields, canonical output, undefined-value rejection and stored event/snapshot JSON. Verify existing storage types remain correct and health updates preserve execution state.

Existing regression anchors:

- `tests/Aubot.RCS.Tests/Serialization/EnumContractCompatibilityTests.cs` checks historical JSON, EF storage and backend/frontend catalog agreement.
- `tests/Aubot.OMC.Tests/Application/Queries/EquipmentHealthProjectionTests.cs` checks separation of equipment health and crane execution.
- `tests/Aubot.MockWms.Tests/EnumFileCompatibilityTests.cs` checks historical mock file formats.
- `src/Aubot.WCS.WebApp/src/app/features/omc/utils/realtime-enum-codec.spec.ts` checks snapshot/SignalR enum boundaries.

Finish with a residual-reference search for removed wrapper names, obsolete enum namespaces and string-based assignments/comparisons in the changed vocabulary. Report the checks actually run; avoid unrelated formatting or rewriting files that contain other pending work.
