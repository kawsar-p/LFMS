# LFMS - Local Development Setup

This version is configured to run locally with SQL Server LocalDB.

## Connection
- Server: `(localdb)\MSSQLLocalDB`
- Database: `LFMS_LocalDb`
- Authentication: Windows/Trusted Connection
- Web URL: `http://localhost:5180`

## Run in Visual Studio
1. Open `LFMS.sln`.
2. Make sure SQL Server LocalDB is installed and running.
3. Build > Rebuild Solution.
4. Run the `LFMS` profile.
5. The application should open at `http://localhost:5180`.

The application creates the local database on first run and seeds the default roles, categories, and admin account.

## Default Admin
- Email: `admin@lostfound.local`
- Password: `Admin123`

Change the admin password after first login.

## Important
This local version no longer uses the Somee SQL Server connection. Your existing Somee connection can be restored later for deployment.
