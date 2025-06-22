# PhotoSync.Tests

Comprehensive test suite for the PhotoSync application.

## Test Structure

### Unit Tests
- **Commands/** - Tests for command handlers (Import, Export, Azure operations)
- **Services/** - Tests for service implementations (Database, File, Azure Storage)
- **Models/** - Tests for data models and DTOs
- **Configuration/** - Tests for configuration and settings

### Integration Tests
- **Integration/** - Database integration tests requiring LocalDB
- **EndToEnd/** - Complete workflow tests

### Test Helpers
- **Fixtures/** - Base classes and test infrastructure
- **Helpers/** - Utility methods for test data generation

## Running Tests

### All Tests
```bash
dotnet test
```

### Specific Category
```bash
# Unit tests only
dotnet test --filter "Category!=Integration"

# Integration tests only
dotnet test --filter "Category=Integration"
```

### With Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Prerequisites

### For Unit Tests
- .NET 8.0 SDK
- No additional requirements

### For Integration Tests
- SQL Server LocalDB or SQL Server instance
- Azurite (for Azure Storage tests)

### Starting Azurite
```bash
# Install globally
npm install -g azurite

# Start Azurite
azurite --silent --location c:\azurite --debug c:\azurite\debug.log
```

## Test Patterns

### Mocking
- Uses Moq for dependency mocking
- Interfaces are mocked for unit tests
- Integration tests use real implementations

### Assertions
- Uses FluentAssertions for readable test assertions
- Custom assertions for domain-specific validations

### Test Data
- TestDataHelper provides methods for generating test files
- In-memory databases for fast unit tests
- Temporary directories cleaned up after tests

## Writing New Tests

### Unit Test Template
```csharp
public class ServiceNameTests
{
    private readonly Mock<IDependency> _mockDependency;
    private readonly ServiceName _service;

    public ServiceNameTests()
    {
        _mockDependency = new Mock<IDependency>();
        _service = new ServiceName(_mockDependency.Object);
    }

    [Fact]
    public async Task MethodName_Scenario_ExpectedResult()
    {
        // Arrange
        _mockDependency.Setup(x => x.Method()).Returns(value);

        // Act
        var result = await _service.MethodAsync();

        // Assert
        result.Should().Be(expected);
    }
}
```

### Integration Test Template
```csharp
public class IntegrationTests : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        // Setup test environment
    }

    public async Task DisposeAsync()
    {
        // Cleanup
    }

    [Fact]
    public async Task Integration_Scenario_Works()
    {
        // Test with real dependencies
    }
}
```

## CI/CD Integration

The test project is configured to work with common CI/CD systems:
- GitHub Actions
- Azure DevOps
- Jenkins

Test results are output in standard formats for integration with build pipelines.