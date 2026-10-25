# Arranger – Implementation Plan

Arranger replaces Doodle (finding a common time) and Scred (sharing expenses) with a single self-hosted web app.
One **arrangement** has a GUID, a title and two tabs: **Schedule** and **Expenses**. A group can use either tab or
both; an unused tab simply stays empty.

## 1. Decisions so far

| Topic | Decision |
|---|---|
| Frontend | Blazor WebAssembly (phone usability is the priority) |
| Backend | One ASP.NET Core host: Orleans silo + REST API + SignalR hub + cookie auth + serves the WASM files |
| Concurrency | One grain per arrangement. Orleans runs one call at a time per grain, so no locking code |
| Persistence | PostgreSQL through EF Core and `Faemiyah.Repository`. No Orleans grain storage. The grain is the only writer and keeps the state in memory |
| Atomicity | Each grain command ends in **one** commit: a single repository call, or one `DbContext` + one `SaveChangesAsync()` for the rare multi-row commands. No `TransactionScope` |
| Realtime | SignalR hub, one group per arrangement. Grain pushes a full snapshot after each successful commit. Clients join the group, then fetch state; on reconnect they rejoin and refetch |
| Authentication | Own `User` entity + `PasswordHasher<User>` + cookie authentication (no ASP.NET Identity stores), local username + password, no email. Site registration is open, with a configuration switch to close it |
| User settings | Handle (username), password, profile picture, time zone, default currency |
| Access | Invite link `https://arranger.faemiyah.fi/{guid}`. Any logged-in user with the link can view. Joining is open until an admin closes participation |
| Roles | Members of an arrangement can edit everything in it, including any expense (used among friends). Admins (the creator, plus members the creator promotes) additionally manage the arrangement |
| Time options | Stored in UTC, shown in each viewer's own time zone; whole-day options are flagged, start at UTC midnight with no end, and are shown as the UTC date only |
| Currencies | Expenses recorded in the currency paid. Arrangement has a base currency (initially the creator's default currency) and member-editable exchange rates; settlement converts with those rates. A new expense defaults to the currency of the previous expense. Automatic rates and per-expense rates come later |
| Transport security | Plain HTTP inside; TLS terminated by the reverse proxy (nginx/Apache) |
| Deployment | Docker Compose. PostgreSQL either as a container or the host's system instance (connection string only) |

## 2. Topology

```
Browser (Blazor WASM)
   │  HTTPS (TLS terminated at nginx/Apache)
   ▼
Reverse proxy ── WebSocket upgrade enabled, X-Forwarded-* headers
   │  HTTP
   ▼
Arranger.Server (single process, single node in v1)
   ├── Static files: WASM app (precompressed via MapStaticAssets)
   ├── Account pages (static server-rendered: login, register, change password)
   ├── /api/*            Minimal API, [Authorize], commands + queries
   ├── /hubs/arrangement SignalR hub, [Authorize], server → client push only
   └── Orleans silo (localhost clustering)
         └── ArrangementGrain (key = arrangement GUID) ──► PostgreSQL
```

* Everything is served from the same origin, so the auth cookie works for the API and the hub, and no CORS is needed.
* Multi-node later: Orleans ADO.NET clustering on the same PostgreSQL + SignalR Redis backplane + sticky sessions.
  No grain code changes.

## 3. Solution structure

The current `src\ArrangerClient` is an untouched template using Interactive Server mode. Replace it with a template
generated for WebAssembly + Individual accounts, then reorganize:

```
dotnet new blazor -n Arranger -int WebAssembly -au Individual --all-interactive
```

| Project | Contents | References |
|---|---|---|
| `Arranger.Server` | Host: cookie auth, API endpoints, hub, Orleans silo, forwarded headers, rate limiting | all below |
| `Arranger.Client` | WASM UI, typed API client, hub client, state container | Contracts, Faemiyah.Ui (NuGet package) |
| `Arranger.Contracts` | Plain request/response records, snapshot types, hub client interface, enums, currency list, shared rules and expense math (`ExpenseMath`: splits, balances, suggested payments). **No Orleans dependency** (it ships to the browser) | – |
| `Arranger.Grains.Abstractions` | `IArrangementGrain`, command result types | Contracts |
| `Arranger.Grains` | `ArrangementGrain`, notifier, validation | Abstractions, Data |
| `Arranger.Data` | EF entities, `ArrangerContext` (incl. users), migrations, repositories | Faemiyah.Repository (NuGet package) |
| `tests\Arranger.UnitTests` | Expense math, validation, client formatting | |
| `tests\Arranger.IntegrationTests` | Grain + DB tests with Orleans `InProcessTestCluster` and Testcontainers PostgreSQL (`postgres:17-alpine`, as in Faemiyah.Repository) | |

Notes:

* Namespaces/assemblies `Faemiyah.Arranger.*`, matching the other Faemiyah projects.
* Persistence comes from the `Faemiyah.Repository` NuGet package (the `Faemiyah.Repository.*` namespaces),
  referenced from `src\Arranger.Data`.
* Generic UI components come from the `Faemiyah.Ui` NuGet package, referenced from `src\Arranger.Client`.
  Before using its inputs for realtime data, make them sync value parameters in `OnParametersSet`
  (most only read them in `OnInitialized`).
* Tests use NUnit 5, AwesomeAssertions and bUnit for components, as in Faemiyah.Repository and Faemiyah.Ui.
* Orleans 10.x (10.4.0 current), .NET 10, Npgsql EF Core 10.
* Serialization is System.Text.Json everywhere, configured as in BtDamageResolver. Orleans serializes every
  `Faemiyah.Arranger*` type with it (`AddSerializer(s => s.AddJsonSerializer(isSupported: namespace prefix,
  jsonSerializerOptions: CreateJsonSerializerOptions()))`). The HTTP API, SignalR and the WASM client apply the same
  `JsonConfiguration.ApplyDefaultJsonSerializerOptions` from Contracts: nulls omitted, case-sensitive names, string
  enums. The API and the client both start from the Web defaults (camelCase), so their property names match.
  No Orleans attributes anywhere, so nothing Orleans-specific reaches the WASM bundle.
* Render mode: global `InteractiveWebAssemblyRenderMode(prerender: false)`. Prerendering would require server-side
  implementations of every client service and adds nothing here. Account pages stay static server-rendered,
  as in the template.

## 4. Data model (PostgreSQL)

All app entities derive from `EntityBase<long>` (`Id`, `xmin` `ConcurrencyToken`, `UpdateTime`).
`User` is `EntityBaseWithUuid<long>` like the other entities: foreign keys use the `long` `Id`, and the `Guid`
`UniqueId` is the only user ID exposed outside the server (claims, API, hub). `UserName` is the visible handle; case is
preserved, but uniqueness and login are case-insensitive via the computed `NormalizedUserName` column.

| Entity | Fields | Constraints |
|---|---|---|
| `User` (`EntityBaseWithUuid<long>`) | `UserName` (3-32 chars of `A-Za-z0-9._-`), `NormalizedUserName` (computed, upper-case), `PasswordHash`, `SecurityStamp`, `CreatedAt`, `TimeZoneId` (IANA, initially from the browser at registration), `DefaultCurrency` (ISO 4217), `ProfilePicture?` (`bytea`), `ProfilePictureContentType?` | unique `NormalizedUserName` |
| `Arrangement` (`EntityBaseWithUuid<long>`) | `UniqueId` (the link GUID), `Title`, `Description`, `OwnerUserId`, `CreatedAt`, `AllowNewUsers`, `AllowNewTimes` (when off, only admins may add time options), `IsScheduleDisabled`, `IsExpensesDisabled`, `FinalTimeOptionId?` (the picked time; set null if the option is deleted), `BaseCurrency` (ISO 4217) | unique `UniqueId` |
| `ArrangementMember` | `ArrangementId`, `UserId`, `JoinedAt`, `IsAdmin` (the owner is always an admin) | unique (`ArrangementId`, `UserId`) |
| `TimeOption` (`TemporalEntityBase<long>`) | `ArrangementId`, `DateBegin` (required by the grain), `DateEnd?` (inherited, `timestamptz`, UTC, truncated to the minute), `IsWholeDay` (`DateBegin` is a UTC midnight naming the date, `DateEnd` is null), `CreatedByUserId` | cascade delete votes; duplicates refused by the grain; `DateBegin`/`DateEnd` indexes inherited |
| `Vote` | `TimeOptionId`, `UserId`, `Answer` (Yes/Maybe/No, stored as text) | unique (`TimeOptionId`, `UserId`) |
| `Expense` | `ArrangementId`, `Description`, `Amount` (`numeric(19,4)`), `Currency`, `PaidByUserId`, `Date`, `IsSettlementPayment` (display only) | cascade delete participations |
| `ExpenseParticipation` | `ExpenseId`, `UserId` | unique (`ExpenseId`, `UserId`) |
| `ExchangeRate` | `ArrangementId`, `Currency`, `Rate` (`numeric(19,8)`, units per 1 base unit, null until known; a row makes the currency available), `LossPercent` (`numeric(5,2)`) | unique (`ArrangementId`, `Currency`) |

* Arrangement deletion cascades to everything, so one delete statement is atomic.
* Time options are instants in UTC. The creator enters them in their own time zone; each viewer sees them converted
  to their own time zone setting. A time option always has a `DateBegin`, although the base class allows null.
* Whole-day options have `IsWholeDay = true`, `DateBegin = D 00:00:00Z` and `DateEnd = null` (e.g. 2022-10-22 00:00,
  no end). The user picks only a date, and every viewer sees just that UTC date, with no time zone conversion.
  The grain validates that `DateBegin` is a UTC midnight and `DateEnd` is null. The flag disambiguates this from a
  timed option without an end. The temporal repository's validity queries aren't used for time options.
* Profile pictures are stored in a separate `UserPicture` row (one per user, cascade on user delete), so logins never
  load them. The client scales them to 256 px before upload. The server accepts PNG, JPEG or WebP (checked by file
  signature, never by the claimed type), at most 256 KB, read with a bounded buffer. They are served from
  `/api/users/{id}/picture` with `Cache-Control: private, no-cache`, an `xmin` ETag and `nosniff`. A user without a
  picture gets a generated SVG initial with a hue derived from the user ID.
* Balances and settlements are **computed**, never stored.
* Users live in the same `ArrangerContext` and are accessed only through Faemiyah.Repository repositories, like
  everything else; there is no separate `AddDbContext` registration.
* Read-only projections (profiles, "my arrangements", pictures) go through small query classes in
  `Arranger.Data\Queries` that select only the needed columns.

### Repository results

Faemiyah.Repository write methods return `RepositoryResult<T>(OperationStatus Status, T? Value)` instead of null or
exceptions. `OperationStatus` is `Found`, `Added`, `Updated`, `Deleted` (success, `Value` set) or `NotFound`,
`Duplicate` (unique constraint), `Conflict` (`xmin` mismatch or removed meanwhile). The repository catches
concurrency and unique violations itself and clears the change tracker on a failed save. Programming errors (an
ambiguous predicate, an identity mismatch) and infrastructure failures still throw, and the app lets them fail fast.
Reads stay nullable. Callers therefore need no try/catch: e.g. registration maps `Duplicate` to "name taken".

### Commands and their writes

| Command | Write | Mechanism |
|---|---|---|
| Join | 1 member row | repository |
| Grant/revoke admin | 1 member row | repository |
| Close/reopen participation | 1 arrangement row | repository |
| Add/remove time option | 1 row (votes cascade) | repository |
| Set vote | 1 row upsert | repository `AddOrUpdateAsync` |
| Pick a time, enable/disable tabs, edit title | 1 arrangement row | repository |
| Add expense | expense + participation rows | repository (`AddAsync` inserts the graph) |
| Edit expense | expense row, participations added/removed | `ExpenseRepository.ReplaceAsync`, one `SaveChangesAsync` |
| Delete expense | 1 row (participations cascade) | repository |
| Set/remove exchange rate | 1 row upsert or delete | repository |
| Change base currency | arrangement row + clear rates | `ArrangementRepository.UpdateClearingRatesAsync`, one `SaveChangesAsync` |
| Leave | member row + the member's votes | `ArrangementMemberRepository.DeleteWithVotesAsync`, one `SaveChangesAsync` |
| Create arrangement | arrangement + owner membership | `DbContext` + one `SaveChangesAsync` |

## 5. Grain design

### `IArrangementGrain` (key: arrangement GUID)

```csharp
Task<CommandResult> CreateAsync(Guid userId, CreateArrangementRequest request);
Task<CommandResult> GetSnapshotAsync(Guid userId);
Task<CommandResult> JoinAsync(Guid userId);
Task<CommandResult> LeaveAsync(Guid userId);
Task<CommandResult> SetParticipationOpenAsync(Guid userId, bool open);
Task<CommandResult> SetTabsAsync(Guid userId, SetTabsRequest request);
Task<CommandResult> SetAdminAsync(Guid userId, Guid memberUserId, bool isAdmin);
Task<CommandResult> UpdateDetailsAsync(Guid userId, UpdateArrangementDetailsRequest request);
Task<CommandResult> DeleteAsync(Guid userId);
Task RefreshMemberAsync(Guid memberUserId);

Task<CommandResult> AddTimeOptionAsync(Guid userId, AddTimeOptionRequest request);
Task<CommandResult> RemoveTimeOptionAsync(Guid userId, long timeOptionId);
Task<CommandResult> SetVoteAsync(Guid userId, long timeOptionId, VoteAnswer answer);
Task<CommandResult> PickTimeAsync(Guid userId, long? timeOptionId);

Task<CommandResult> AddExpenseAsync(Guid userId, ExpenseRequest request);
Task<CommandResult> UpdateExpenseAsync(Guid userId, long expenseId, ExpenseRequest request);
Task<CommandResult> DeleteExpenseAsync(Guid userId, long expenseId);
Task<CommandResult> SetBaseCurrencyAsync(Guid userId, string currency);
Task<CommandResult> SetExchangeRateAsync(Guid userId, string currency, SetExchangeRateRequest request); // adds the currency; null rate = unknown
Task<CommandResult> RemoveCurrencyAsync(Guid userId, string currency); // refused while an expense uses it
```

* `userId` always comes from the authenticated cookie in the host, **never from the request body**.
* `CommandResult` = error code (`None`, `NotFound`, `NotMember`, `Forbidden`, `Invalid`, `Conflict`), message, and
  on success the full `ArrangementSnapshot` including its `Version`.
* Refusals are returned, not thrown: checks produce a failed `CommandResult` before any state changes. Writes go to
  the database before memory, so an unexpected exception leaves memory intact and simply propagates. A write the
  repository refuses (`Conflict`/`Duplicate`/`NotFound`) means memory is stale: the grain deactivates and returns
  `Conflict`. No custom exceptions or grain call filters.
* In-memory state and its permission checks (`CheckExists`/`CheckMember`/`CheckAdmin`/`CheckOwner`, returning the
  refusal or `null`) live in `ArrangementState`, so the grain methods contain only the command logic.
* **Every** authorization and validation rule lives in the grain. The API layer is plumbing only.

### Lifecycle and command flow

1. `OnActivateAsync`: load the arrangement graph and member handles. If it doesn't exist, the grain stays in a
   "not found" state; only `CreateAsync` succeeds.
2. Command: authorize → validate → write to the database (one commit) → apply to memory → increment `Version` →
   notify.
3. A repository result other than success (`Conflict`, `Duplicate`, `NotFound`: the database moved on without this
   activation, e.g. a duplicate activation) calls `DeactivateOnIdle()` and refuses with `Conflict`, so the next call
   reloads. Infrastructure exceptions deactivate through the filter and fail the request.
4. Idle deactivation uses Orleans defaults. No reminders, no grain storage.

### Versioning

`Version = { Epoch: Guid, Sequence: long }`. `Epoch` is generated on activation and `Sequence` increments per
successful command. Keeping it in memory means no extra database write per command.

Client rule: same epoch → apply if `Sequence` is greater than the current one; different epoch → apply and treat as
authoritative (the grain was reactivated, and the snapshot is complete anyway).

### Notification

After a successful commit the grain calls `IArrangementNotifier` (`ChangedAsync(snapshot)`, `DeletedAsync(id)`). The
server's `HubArrangementNotifier` sends to the SignalR group of the arrangement through
`IHubContext<ArrangementHub, IArrangementClient>`; the co-hosted silo shares the server's DI container. A group send
only queues messages on local connections; it doesn't wait for clients. Notification failures are logged and never
fail the command. No-ops and refusals don't notify. Calls are not reentrant, so pushes leave in commit order.

The snapshot is the whole arrangement (both tabs). Balances and suggested payments are computed by the client with the shared `ExpenseMath`. That's small at this scale. If expense lists
grow large, split it into per-tab snapshots later.

### "My arrangements"

`GET /api/arrangements` reads directly from the database (`ArrangementMember` join). This is read-only, bypasses
grains, and is allowed per the "grains are the only writers" rule.

### User settings

User settings are not arrangement state, so they don't go through grains: the endpoints update them through the
user repository (one user row per call, `xmin` concurrency check; `Duplicate` means the handle is taken). Snapshots
carry member IDs and handles; clients load profile pictures by user ID. On a handle change the endpoint reissues the
cookie (the handle is a claim, so the client reloads) and calls `RefreshMemberAsync` on each of the user's
arrangement grains, which update the cached handle and notify.

## 6. API and hub

All under `RequireAuthorization()`. JSON only (camelCase, case-sensitive). Errors as `ProblemDetails`:
`CommandResult.ToHttpResult()` maps `NotFound` → 404, `NotMember`/`Forbidden` → 403, `Invalid` → 400 and
`Conflict` → 409; success is 200 with the snapshot, or 204 without one. Non-GET requests must carry
`X-Requested-With: Arranger` (CSRF guard, since SameSite=Lax treats sibling subdomains as same-site).

| Method | Route | Grain call |
|---|---|---|
| GET | `/api/arrangements` | DB query (mine) |
| POST | `/api/arrangements` | new GUID → `CreateAsync` |
| GET | `/api/arrangements/{id}` | `GetSnapshotAsync` |
| PUT / DELETE | `/api/arrangements/{id}` | `UpdateDetailsAsync` / `DeleteAsync` |
| POST | `/api/arrangements/{id}/join` | `JoinAsync` |
| POST | `/api/arrangements/{id}/leave` | `LeaveAsync` |
| PUT | `/api/arrangements/{id}/participation` | `SetParticipationOpenAsync` |
| PUT | `/api/arrangements/{id}/tabs` | `SetTabsAsync` |
| PUT | `/api/arrangements/{id}/members/{userId}/admin` | `SetAdminAsync` |
| POST | `/api/arrangements/{id}/time-options` | `AddTimeOptionAsync` |
| DELETE | `/api/arrangements/{id}/time-options/{optionId}` | `RemoveTimeOptionAsync` |
| PUT | `/api/arrangements/{id}/time-options/{optionId}/vote` | `SetVoteAsync` |
| PUT | `/api/arrangements/{id}/pick` | `PickTimeAsync` (`null` clears) |
| POST | `/api/arrangements/{id}/expenses` | `AddExpenseAsync` |
| PUT / DELETE | `/api/arrangements/{id}/expenses/{expenseId}` | `UpdateExpenseAsync` / `DeleteExpenseAsync` |
| PUT | `/api/arrangements/{id}/base-currency` | `SetBaseCurrencyAsync` |
| PUT | `/api/arrangements/{id}/rates/{currency}` | `SetExchangeRateAsync` (adds; `null` rate = unknown) |
| DELETE | `/api/arrangements/{id}/rates/{currency}` | `RemoveCurrencyAsync` |
| GET / PUT | `/api/me/settings` | User repository (time zone, default currency, handle) |
| PUT / DELETE | `/api/me/picture` | Picture repository (raw image body) |
| GET | `/api/users/{userId}/picture` | DB read |
| GET | `/api/time-zones` | IANA zones ordered by current offset (`Etc/*` excluded) |

Password changes use the static server-rendered account page `/Account/Manage/ChangePassword`. Logout is
`POST /Account/Logout`. Endpoints are minimal APIs, one static class per endpoint group (e.g. `AccountEndpoints`).

**Hub** `/hubs/arrangement` (`[Authorize]`):

* Client → server: `Subscribe(Guid id)` adds the connection to the group first, then returns the current snapshot, or
  null (and leaves the group) when the arrangement doesn't exist. Joining first means no change can fall between the
  snapshot and the first push, so there is no separate `GET`. There is no `Unsubscribe`: each page has its own
  connection and disposes it on leaving.
* Server → client (`IArrangementClient`): `ArrangementChanged(ArrangementSnapshot)`, `ArrangementDeleted(Guid)`.

**Client connection rules** (`ArrangementLiveConnection`): pushes are not stored, so the client calls `Subscribe` again
on every (re)connect (groups are bound to the connection ID and are lost on reconnect) and whenever the page becomes
visible again (phone wake; `wwwroot\js\visibility.js`). Snapshots older than the one shown are discarded.
`ForeverRetryPolicy` reconnects forever (0, 2, 5, 10, then every 30 seconds); the first start retries the same way, and
a 401 sends the user to the login page. The page shows "Reconnecting…" while not live.

## 7. Domain rules

### Permissions

| Action | Who |
|---|---|
| View | any logged-in user with the GUID |
| Join | any logged-in user, while participation is open |
| Leave | members except the owner, unless they paid or share any expense; their votes are removed |
| Add/remove time options, vote (own), add/edit/delete any expense, edit exchange rates | members |
| Close/reopen participation, enable/disable tabs, pick a time, edit title, change base currency | admins |
| Grant/revoke admin, delete arrangement | owner (the owner's admin status cannot be revoked) |

### Defaults

* New arrangement: base currency = the creator's default currency.
* New expense: currency = the currency of the most recently added expense in the arrangement, or the base currency
  if there are none. Payer = me, participants = all members.
* Times are entered and shown in the user's time zone setting, and sent to the server as UTC. Whole-day options
  are entered with a date picker only and shown as the UTC date.

### Expenses and settlement (`ExpenseMath` in Contracts)

1. Equal split in the currency's smallest unit (ISO 4217 minor units, `Currencies.Decimals`): every share is
   `floor(amount / n)`, and the leftover units go one each to participants by user ID, so shares always sum exactly
   to the amount. Amounts may not have more decimals than the currency.
2. Balance per user **per currency** = paid − owed. Always shown, needs no rates.
3. Base-currency view: `Σ balance_c / rate_c` (base currency rate = 1), rounded to the base currency's unit. A rate
   is the units of the currency 1 base unit buys, e.g. 1 EUR = 160 JPY (> 0, ≤ 10⁹, ≤ 8 decimals), with an optional
   conversion loss p (0–50 %, ≤ 2 decimals, default 0): the effective rate is `rate × (1 − p/100)`, i.e. you get that
   much less of the currency. When every used currency has a rate,
   balances and suggestions are shown in the base currency only, so a payment in any currency settles automatically.
   Otherwise the view names the missing rates and falls back to per-currency balances and suggestions.
4. Suggested payments: repeatedly the largest debtor pays the largest creditor (at most n − 1 payments; ties by user
   ID). Rounding leftovers smaller than the currency's unit are ignored. Computed whenever shown, never stored.
5. A payment is a plain expense: payer = debtor, the creditor as the only participant, which clears the debt.
   "Record payment" on a suggestion posts one; members can also record any payment themselves, or edit and delete
   them like any expense. `IsSettlementPayment` only colours the row.
6. Changing the base currency clears all rates (one `SaveChangesAsync`), because they were relative to the old base.

## 8. Client (WASM)

* Routes: `/` (New button, then my arrangements), `/new` (title, description, which tabs to enable),
  `/{Id:guid}` and `/{Id:guid}/{schedule|expenses|settings}` (arrangement), `/settings` (user settings), account
  pages under `/Account/*`.
  Unauthenticated access to `/{guid}` redirects to login with `ReturnUrl`.
* Arrangement page: header (title, description, Join button for non-members), tabs **Schedule**, **Expenses** and
  **Settings** (the `Faemiyah.Ui` tab components, Settings pushed to the right; each tab has its own URL for
  bookmarking). Without a tab in the URL the only enabled tab opens, otherwise Schedule. A disabled tab stays in the
  tab bar and shows its content read-only; the grain refuses changes.
* Settings (`ArrangementSettings`, all members): link with a copy button, members (owner promotes admins); admins
  also get the participation and tab switches and the details form; the owner can delete, other members can leave.
  Currencies (`CurrencySettings`): base currency (admins; confirmed when rates exist; clears the rates, keeps the
  currencies and adds the old base), then a row ("1 BASE = [rate] X, loss [p] %, ×") per currency added from the
  "Add a currency" combobox; × is disabled while an expense uses the currency. Editable by members while the expenses are
  enabled. The expense form only offers the base and the added currencies (plus an edited expense's own), picked with a
  `Faemiyah.Ui` combobox.
* Schedule (`Schedule` component, formatting in `ScheduleFormat`): options × members grid, options ordered by time.
  Members edit only their own row while the schedule is enabled; tapping a cell cycles none → Yes → Maybe → No → Yes, each
  tap one `PUT`. The footer shows yes (maybe) counts, a remove button for members (confirmed when the option has
  answers) and a Pick toggle for admins. A pick is only a highlighted proposal that everyone sees live; the admin's
  "Close schedule" button (confirmed when nothing is picked) disables the schedule, which confirms the pick and shows
  it in a banner with a Reopen button for admins. The pick survives reopening. "Add time" opens the add form. Timed options are entered and shown in the user's time zone (an end at or before the
  start is the next day, shown as `(+1)`; times skipped by DST are refused); whole-day options show their date.
* Expenses (`Expenses` component): "Add expense" form (description, amount, currency defaulting to the last added
  expense's currency or the base currency, payer defaulting to me, date, participant chips with all members selected,
  a payment checkbox); list by date with edit and delete (confirmed), payments coloured, the amount in two columns (the paid amount when not in the base currency, then the base amount or "?" without a rate); balances per currency;
  a balance table per member (Spent = paid, Received = shares, Balance = Spent − Received); suggested payments with "Record payment"; balances in the base currency when no rate is missing, otherwise a
  warning linking to ⚙. Read-only when the tab is disabled.
* Services: `ArrangerApi` (`HttpClient`, problem details to messages, 401 to login), `CurrentUser` (settings, time
  zone conversion), `ArrangementLiveConnection` (hub, subscribe/reconnect/refetch). The arrangement page shows the
  newest snapshot from either a command response or a push (version check); form fields are only overwritten when the
  details actually changed.
* Mobile-first layout (Bootstrap from the template); the time-option grid scrolls horizontally on small screens.

## 9. Hardening and deployment

* `ForwardedHeaders` limited to the proxy's address; cookie `Secure` based on the forwarded scheme;
  `SameSite=Lax` (the default) plus JSON-only endpoints blocks cross-site form posts.
* Authentication: password length 8-128, `PasswordHasher<User>` (rehash on login when needed), optional registration
  switch (`Registration:Enabled`). Each user has a `SecurityStamp` claim, revalidated against the database when the
  cookie is older than 5 minutes; changing the password rotates it and signs out other sessions. `/api` and `/hubs`
  get 401/403 instead of login redirects.
* No account lockout (it would let anyone lock out a known handle). Instead, failed logins are logged as
  `Arranger failed login for user {name} from {ip}` for fail2ban to ban the IP, and the account pages have a per-IP
  rate limit (10 POSTs per minute). The fail2ban filter and jail example go to `docs\` in M6.
* Known proxies come from `ReverseProxy:KnownProxies` (list of IP addresses).
* Rate limiting per user (fixed window) on `/api`, a stricter limit on login/register; request body size limit.
* Grain-side limits: title ≤ 200, description ≤ 2000, ≤ 100 members, ≤ 100 time options, ≤ 2000 expenses,
  amount 0 < x ≤ 10⁹, rate > 0, ISO 4217 currency codes only.
* Logging with structured `ILogger` to the console as `{UTC time} - [{level}] - {category} - {message}` (as in BtDamageResolver); health endpoint `/healthz`.
* nginx example config in `docs\` (WebSocket `Upgrade`/`Connection` headers, `proxy_read_timeout` above the SignalR
  keep-alive, forwarded headers). Apache equivalent uses `mod_proxy_wstunnel`.
* Deployment: Docker Compose behind the host's reverse proxy. The Compose file includes a PostgreSQL service that
  can be left out in favour of the host's PostgreSQL by changing the connection string.

## 10. Milestones

| # | Milestone | Done when |
|---|---|---|
| M0 | Restructure solution: WASM template, own cookie auth, projects from section 3, PostgreSQL, Faemiyah.Repository wired, Orleans silo running | Register/login works against PostgreSQL; solution builds; empty test projects run |
| M1 | Arrangement core: entities + migration, user settings (time zone, default currency, picture), grain create/get/join, admins and closing participation, API, Home, Settings and Arrangement pages | Create an arrangement, open `/{guid}` as another user, join; joining fails after an admin closes participation |
| M2 | Realtime: hub, groups, client live connection, versioning, reconnect/refetch | Two browsers see each other's joins instantly; survives a server restart and a phone sleep |
| M3 | Schedule tab: options, votes, close/reopen | Full Doodle flow with live updates; grain tests for permissions |
| M4 | Expenses tab: CRUD, per-currency balances, settlements, record payment | Unit tests for split/rounding/settlement; end-to-end flow |
| M5 | Base currency + exchange rates, base-currency settlement, leaving | Mixed-currency scenario settles correctly |
| M6 | Hardening and deployment: rate limits, input limits, proxy config, Docker Compose, fail2ban filter | Running behind nginx at the target domain |

**Later**: automatic exchange rates from an external API, per-expense rates, unequal splits, comments, removing
members, cleanup of old arrangements, multi-node.

## 11. Open questions

None at the moment. The earlier questions (permissions, registration, deployment, time options) are resolved in
sections 1 and 7.
