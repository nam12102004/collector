# Worked example — Lead interaction & task popups

A filled spec-table for the Lead detail drawer's two dialogs (REQ-0008 §5.2 "Ghi nhận tương tác" and REQ-0007 §5.2 "Giao việc"). Note the two dialogs are listed **separately** — merging them is what leaked fields into the wrong popup.

## A. Fields

| Popup | Field | Type | Required | Validation (BR) | DTO | Validator | FE control | Done |
|---|---|---|:--:|---|:--:|:--:|:--:|:--:|
| Ghi nhận tương tác | Loại (type) | dropdown | ✓ | Call/Email/Meeting/Demo/Note/Other | ✓ | ✓ | ✓ | ☑ |
| Ghi nhận tương tác | Thời gian thực hiện | datetime | ✓ | ≤ now (server clock, skew-tolerant); empty → server stamps | ✓ | ✓ | ✓ | ☑ |
| Ghi nhận tương tác | Tóm tắt (subject) | text | ✓ | ≤255 | ✓ | ✓ | ✓ | ☑ |
| Ghi nhận tương tác | Nội dung chi tiết | textarea | ✗ | ≤1000 | ✓ | ✓ | ✓ | ☑ |
| Ghi nhận tương tác | ~~Độ ưu tiên~~ | — | — | **belongs to Giao việc, NOT here** | — | — | absent | ☑ |
| Giao việc | Nội dung (subject) | text | ✓ | ≤255 | ✓ | ✓ | ✓ | ☑ |
| Giao việc | Hạn hoàn thành (due) | datetime | ✓ | not in the past | ✓ | ✓ | ✓ | ☑ |
| Giao việc | Độ ưu tiên | dropdown | ✓ | High/Medium/Low | ✓ | ✓ | ✓ | ☑ |
| Giao việc | Người phụ trách | dropdown | ✓ | default = lead owner; enabled only for Admin/Manager | ✓ | ✓ | ✓ | ☑ |

## B. Business rules

| BR | Rule | Where enforced | Done |
|---|---|---|:--:|
| REQ-0008 BR-03 | Interaction time cannot be in the future | validator (server-clock + skew tolerance) | ☑ |
| REQ-0007 BR-03 | Task due date cannot be in the past | validator + FE | ☑ |
| create-status | Interaction may be created Completed; task must start Planned | service | ☑ |

## C. Endpoints

| Method | Route | Request | Response | Auth | Done |
|---|---|---|---|---|:--:|
| POST | /activities | CreateActivityRequest | ActivityDetailDto | activity.create / task.create by type | ☑ |
| GET | /activities?leadId&activityType=Task | filter | paged | activity.read / task.read | ☑ |

## What the guardrails (§7) would have caught here

- **#2 form reuse**: one shared form served both dialogs → "Độ ưu tiên" showed in the interaction popup. The per-popup Fields table makes the extra row obvious.
- **#3 list vs backend scoping**: the tasks tab queried `/activities?leadId` with no `activityType`; the backend scopes an untyped query to non-task types, so saved tasks never returned. The endpoints table forces the `activityType=Task` query.
- **#4 client timestamps**: the form sent `new Date().toISOString()`; a lagging server clock rejected it as "in the future". The field's validation cell ("server clock; empty → server stamps") encodes the correct behavior.
- **#5 create-status**: the form sent `status=Completed` while the backend required `Planned`. The create-status BR row captures the contract.
