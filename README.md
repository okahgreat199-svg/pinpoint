# Pinpoint

Attendance app I built for CS32005 (Mobile App Development) at Dundee.

The lecturer puts a QR code up on the screen, students scan it with their phones, and they're checked in. Paper registers and normal QR codes are way too easy to cheat (someone takes a photo of the code, sends it to the group chat, and half the class "attends" from bed), so most of the work went into stopping that.

## How it stops cheating

- **The QR code changes every 15 seconds.** Each code is signed by the server, so a screenshot is dead almost straight away.
- **Each student gets one phone.** The phone makes its own key and signs every check-in with it. Switching to a new phone needs the lecturer to approve it, and one phone can't check in two people for the same class.
- **Location is checked.** You have to be on campus or you're rejected. You get flagged if you're far from where the lecturer started the session, your GPS is rubbish, your phone says the location is mocked, or you're way off from where everyone else checked in.
- **Scan and leave doesn't work.** About 70% of the way through the class, everyone who checked in gets a notification asking if they're still there. Don't answer within 10 minutes and you get flagged.

Things like a bad code, not being enrolled, the wrong phone or being off campus reject the check-in outright. GPS stuff only flags it, because GPS is too unreliable to reject people on. The lecturer goes through the flagged ones and marks each person present or absent.

## What's in here

- `api/`: ASP.NET Core API with PostgreSQL and EF Core. All the attendance decisions happen here; the phone just reports what it sees.
- `api/Domain/`: the actual rules (check-in rules, QR tokens, distance maths, timetable stuff, device signatures). No database or HTTP in here, so it's easy to test.
- `api.Tests/`: xUnit tests.
- `frontend/`: the Flutter app (Android) using Riverpod. Lecturers and students use the same app; it shows different screens depending on who logs in.
- `deploy/`: scripts I used to put the API on a Linux VPS behind Caddy (HTTPS) as a systemd service.

## Running it

**API** (you need the .NET SDK and PostgreSQL):

```bash
cd api
cp .env.example .env   # fill in your DB connection, JWT key, seed password etc.
dotnet run             # runs on http://localhost:5080, Swagger at /swagger
```

In dev mode it sets up the database and seeds some demo accounts on its own.

**Tests:**

```bash
dotnet test api.Tests
```

**App** (you need Flutter and an Android phone or emulator):

```bash
cd frontend
flutter pub get
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5080   # 10.0.2.2 = your computer, from the emulator
```

Push notifications won't work unless you hook up your own Firebase project. Everything else works without it.

## What's not done

- iOS. It's Android only for now.
- A geofence around each lecture room (the campus one is done). That would catch someone walking out mid-class; for now the 70% check covers it.
- A web admin page. Admin stuff goes through Swagger.
- Connecting to the uni's real student records.
