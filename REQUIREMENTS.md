# Requirements - Photo Import/Export Console Application

## Functional Requirements

### Core Features
1. **Import Command**: Load JPG images from a folder into a database table
   - Read all `.jpg` files from specified folder
   - Use filename (without extension) as the record code
   - Store binary image data in configured database field
   - Support configurable table and field names

2. **Export Command**: Save images from database to folder as JPG files
   - Retrieve all image records from database
   - Save each image as `<code>.jpg` in specified folder
   - Support configurable table and field names

### Command Line Interface
- `PhotoSync.exe import [folder_path]`
- `PhotoSync.exe export [folder_path]`
- `PhotoSync.exe status` (show database status)
- `PhotoSync.exe test` (test configuration)
- Folder path parameter optional (uses configuration if not provided)

## Technical Requirements

### Architecture
- .NET Core console application targeting Azure Linux App Services
- Entity Framework Core for database access (stored procedures only)
- No LINQ usage - stored procedures exclusively
- No dynamic SQL generation
- Command and connection objects for all database operations

### Database
- SQL Server with T-SQL stored procedures
- Configurable table name and image field name
- Binary data storage (varbinary(max))
- Code field for image identification
- Created/Modified date tracking

### Configuration
- appsettings.json for all configuration
- Azure Key Vault integration for secrets
- Environment-specific configuration support

### Resilience & Monitoring
- Polly for retry policies on database operations
- Serilog for structured logging
- Application Insights integration
- Comprehensive error handling

### Storage
- Azure Storage for blob storage capability (future enhancement)
- Local file system operations for import/export

## Non-Functional Requirements

### Performance
- Handle large image files efficiently
- Batch processing capabilities
- Configurable timeout values
- Memory-efficient file processing

### Reliability
- Retry logic for transient failures
- Transaction support for data consistency
- Graceful error handling and recovery
- Detailed logging for troubleshooting

### Security
- Parameterized stored procedures
- Input validation
- Secure configuration management
- Azure Key Vault integration

### Maintainability
- Clean separation of concerns
- Interface-based design
- Comprehensive unit test coverage
- Clear documentation and comments

## Constraints

### Technical Constraints
- Must use stored procedures only (no LINQ)
- No dynamic SQL generation
- Azure deployment target
- .NET Core framework requirement

### Business Constraints
- JPG format only for import/export
- Single table per operation
- Code field uniqueness requirement
