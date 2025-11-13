# WordWebCMS Migration to ASP.NET Core 8.0

## Overview
This document describes the migration from ASP.NET Web Forms (.NET Framework 4.6.2) to ASP.NET Core 8.0.

## Migration Status: ✅ Core Infrastructure Complete

### Completed Components

#### 1. Project Structure
- ✅ Converted from old `.csproj` format to SDK-style `.csproj`
- ✅ Target framework: `net8.0`
- ✅ Created `Program.cs` with ASP.NET Core middleware pipeline
- ✅ Created `appsettings.json` and `appsettings.Development.json`

#### 2. Dependency Management
- ✅ Resolved LinePutScript dependency issues (using .NET Framework 4.6.2 DLLs in `/lib` folder)
- ✅ Updated MySqlConnector to version 2.3.7
- ✅ Replaced Westwind.Web.Markdown with Markdig 0.37.0
- ✅ All core dependencies resolved

#### 3. Infrastructure Services
- ✅ Created `ApplicationCache` service (replaces `HttpApplicationState`)
- ✅ Created `HttpContextService` (replaces `HttpContext.Current`)
- ✅ Configured dependency injection in `Program.cs`
- ✅ Added middleware to inject services into static classes

#### 4. Function Classes Migration
- ✅ `Conn.cs`: Updated to use `IConfiguration`
- ✅ `Setting.cs`: Updated to use `ApplicationCache` instead of `HttpApplicationState`
- ✅ `Master.cs`: Removed `System.Web` dependencies, uses content root path
- ✅ `Function.cs`: Replaced Westwind with Markdig, removed `System.Web.UI`
- ✅ `User.cs`, `Posts.cs`, `Review.cs`: Removed `System.Web` references
- ✅ All Function classes build successfully (0 errors, 77 nullable warnings)

#### 5. Razor Pages
- ✅ Created Razor Pages infrastructure
  - `Pages/_ViewImports.cshtml`
  - `Pages/_ViewStart.cshtml`
  - `Pages/Index.cshtml` and `Pages/Index.cshtml.cs`
- ✅ Index page working with simplified content
- ✅ Theme integration working (header/footer from `Themes/` directory)

### Remaining Work

#### Web Forms to Razor Pages Conversion
- [ ] Login.aspx → Login.cshtml
- [ ] Post.aspx → Post.cshtml
- [ ] Setup.aspx → Setup.cshtml
- [ ] User.aspx → User.cshtml

#### AJAX Handler Conversion
- [ ] AJAX.ashx → API Controller endpoints

#### Full Functionality Implementation
- [ ] Complete Index page with post listing from database
- [ ] Implement session-based authentication in Razor Pages
- [ ] Implement user login/logout
- [ ] Implement post viewing with comments
- [ ] Implement setup/installation wizard
- [ ] Implement user profile management

## Architecture Changes

### Configuration
| ASP.NET Framework | ASP.NET Core 8.0 |
|-------------------|------------------|
| Web.config | appsettings.json + Program.cs |
| ConfigurationManager | IConfiguration (DI) |

### Caching
| ASP.NET Framework | ASP.NET Core 8.0 |
|-------------------|------------------|
| HttpApplicationState | IMemoryCache + ApplicationCache service |
| Application["key"] | AppCache["key"] |

### HTTP Context
| ASP.NET Framework | ASP.NET Core 8.0 |
|-------------------|------------------|
| HttpContext.Current | IHttpContextAccessor + HttpContextService |
| Request.UserHostAddress | HttpContextService.GetUserHostAddress() |
| Request.Url | HttpContextService.GetRequestUrl() |

### Session Management
| ASP.NET Framework | ASP.NET Core 8.0 |
|-------------------|------------------|
| System.Web.SessionState | ASP.NET Core Session middleware |
| Session["key"] | HttpContext.Session.GetString("key") |

### Page Model
| ASP.NET Framework | ASP.NET Core 8.0 |
|-------------------|------------------|
| Web Forms (.aspx + .aspx.cs) | Razor Pages (.cshtml + .cshtml.cs) |
| Page_Load event | OnGet/OnPost methods |
| Server.Transfer | Redirect() or return Page() |

### Markdown Processing
| ASP.NET Framework | ASP.NET Core 8.0 |
|-------------------|------------------|
| Westwind.Web.Markdown | Markdig with AdvancedExtensions |
| Westwind SanitizeHtml | Custom Regex-based sanitization |

## Build Status
✅ **BUILD SUCCESSFUL**
- 0 Errors
- 77 Warnings (nullable reference types only)

## Dependencies in /lib Folder
The following DLLs are included in the `/lib` folder and must be deployed:
- LinePutScript.dll (v1.3.1 for .NET Framework 4.6.2)
- LinePutScript.SQLHelper.dll (v1.1.7 for .NET Framework 4.6)

These are .NET Framework DLLs that work on .NET 8.0 through compatibility layers.

## Running the Application

### Development
```bash
cd WordWebCMS
dotnet run
```

### Build
```bash
cd WordWebCMS
dotnet build
```

### Publish
```bash
cd WordWebCMS
dotnet publish -c Release -o ./publish
```

## Configuration
Update `appsettings.json` with your database connection strings:
```json
{
  "ConnectionStrings": {
    "connStr": "Server=localhost;port=3306;Database=wwcms;User=root;Password=yourpassword;Charset=utf8;Allow Zero Datetime=True;Pooling=false;Max Pool Size=50;",
    "connUsrStr": "Server=localhost;port=3306;Database=wwcms;User=root;Password=yourpassword;Charset=utf8;Allow Zero Datetime=True;Pooling=false;Max Pool Size=50;"
  }
}
```

## Testing
1. Ensure MySQL database is running
2. Import `setup.sql` to create database schema
3. Update connection strings in `appsettings.json`
4. Run the application
5. Navigate to `http://localhost:5000` (or your configured port)

## Next Steps
1. Complete the migration of remaining pages (Login, Post, Setup, User)
2. Convert AJAX.ashx to API controllers
3. Test all functionality with real database
4. Deploy to production environment
5. Update solution file to remove old Web Forms project references

## Notes
- The project maintains backward compatibility with existing themes in the `Themes/` directory
- Database schema remains unchanged from the original ASP.NET version
- Static files (Picture/, Themes/) are served using ASP.NET Core static files middleware
