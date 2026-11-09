# Interview preparation: explain the code, don't just show it

These are study prompts. Make sure you can demonstrate the behavior before claiming the project as evidence of your own C# experience.

**1. Why use C# and ASP.NET Core?**

The API demonstrates object-oriented domain modeling, typed DTOs, dependency injection, asynchronous database operations, and business logic separated from transport. These are transferable from Java/Spring experience.

**2. Where is the software design?**

The feature is split into HTTP endpoints, application services, domain workflow + validation, EF Core data access, and the browser interface. The domain rules are independently unit tested.

**3. Why a version field?**

Two agents may open the same case and make conflicting edits. An optimistic concurrency token detects stale updates, returns a conflict, and prevents silent overwriting.

**4. What tests did you implement?**

The unit tests validate legal and illegal transitions and SLA deadlines. SQLite-backed tests exercise persistence, input validation, notes, activity history, pagination, and 409 conflicts.

**5. What would you change before production?**

Add authentication/authorization, proper migrations, tenant-aware data access, operational telemetry, protected audit events, backups, and production database infrastructure. Use real customer data only with appropriate privacy controls.

**6. Is this a Microsoft Dynamics 365 integration?**

No. It is an independent customer-case workflow prototype inspired by typical CRM use cases. An actual integration would require Dataverse authentication, official APIs, entity mappings, permissions, and error/retry strategies.

**7. What would you build next?**

Make the business SLAs configurable, add a role-protected API and integration tests, implement an outbox for reliable events, or integrate with a test Dataverse environment through approved APIs.

## Hands-on tasks before GitHub publication

- [ ] Install .NET 10 and run the application locally.
- [ ] Execute all tests and fix any failures on your machine.
- [ ] Create a case and explain every request/response in browser DevTools.
- [ ] Change one transition rule and update its tests.
- [ ] Add a feature of your own (e.g. case categories or tags) and document it.
- [ ] Capture an authentic screenshot of your running dashboard for the README.
- [ ] Commit a series of meaningful changes rather than uploading everything as an unexplained dump.
