# API reference

Base URL for local development: `http://localhost:5055`. JSON enums are strings such as `"High"` or `"InProgress"`.

## Create a case

```http
POST /api/cases
Content-Type: application/json

{
  "title": "PDF generation fails for large batches",
  "description": "A batch with more than fifty invoices sometimes fails to complete.",
  "customerName": "Example Customer",
  "customerEmail": "support@example.com",
  "priority": "High",
  "assignedTo": "Alex"
}
```

Returns **201 Created** with a full case object, a new GUID `id`, human-readable `reference`, `version: 1`, deadline, note list, and activity list. Invalid fields return **400** with an explanation.

## List, filter, and paginate

```http
GET /api/cases?page=1&pageSize=10&search=invoice&priority=High&status=Open
```

All filter parameters are optional. Valid `status`: `Open`, `InProgress`, `WaitingOnCustomer`, `Resolved`, `Closed`. Valid `priority`: `Low`, `Normal`, `High`, `Critical`. `pageSize` must be 1–100. The response contains `items`, `page`, `pageSize`, and `total`.

## Case details

```http
GET /api/cases/REPLACE-WITH-GUID
```

Returns the full case record, latest-first internal notes and activity history. Missing IDs return **404**.

## Change status

```http
PUT /api/cases/REPLACE-WITH-GUID/status
Content-Type: application/json

{
  "status": "InProgress",
  "version": 1,
  "actor": "Alex"
}
```

- A valid transition returns **200** and an incremented `version`.
- A stale version returns **409 Conflict**. Fetch the latest case before retrying.
- A disallowed transition returns **422 Unprocessable Entity**.
- Invalid input returns **400**.

Legal transitions:

```text
Open                 -> InProgress
InProgress           -> Open | WaitingOnCustomer | Resolved
WaitingOnCustomer    -> InProgress | Resolved
Resolved             -> InProgress | Closed
Closed               -> InProgress
```

## Assign or unassign

```http
PUT /api/cases/REPLACE-WITH-GUID/assignment
Content-Type: application/json

{
  "assignedTo": "Mira",
  "version": 2,
  "actor": "Alex"
}
```

Use an empty string or `null` for `assignedTo` to unassign. Repeating an unchanged assignment returns **422**.

## Add internal note

```http
POST /api/cases/REPLACE-WITH-GUID/notes
Content-Type: application/json

{
  "author": "Mira",
  "message": "Investigating the document template mapping.",
  "version": 3
}
```

Creates both a persisted note and a timestamped `note_added` audit event.

## Dashboard and export

```http
GET /api/dashboard
GET /api/cases/export.csv
GET /health
```

`/api/dashboard` returns totals for each status, active critical cases, and overdue cases. `/export.csv` downloads all cases with basic spreadsheet formula escaping. `/health` checks database connectivity and returns HTTP **503** if the database is unavailable.

## SLA calculation

| Priority | Demo SLA |
| --- | --- |
| Critical | 4 hours |
| High | 24 hours |
| Normal | 72 hours |
| Low | 168 hours (7 days) |

SLA is measured from the case creation timestamp in UTC. For simplicity this demo uses **calendar hours, not business hours**. Statuses `Resolved` and `Closed` are excluded from the overdue count.
