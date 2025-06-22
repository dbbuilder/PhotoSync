#!/bin/bash
# PhotoSync Build and Test Script for Linux/Mac

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Parse arguments
SKIP_INTEGRATION=false
COVERAGE=false
VERBOSE=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-integration)
            SKIP_INTEGRATION=true
            shift
            ;;
        --coverage)
            COVERAGE=true
            shift
            ;;
        --verbose)
            VERBOSE=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

echo -e "${CYAN}PhotoSync Build and Test Script${NC}"
echo -e "${CYAN}================================${NC}"
echo ""

# Function to check command result
check_result() {
    if [ $? -ne 0 ]; then
        echo -e "${RED}✗ $1 failed${NC}"
        exit 1
    else
        echo -e "${GREEN}✓ $1 completed${NC}"
    fi
}

# Clean previous build artifacts
echo -e "${YELLOW}Cleaning previous build artifacts...${NC}"
dotnet clean --verbosity quiet
check_result "Clean"
echo ""

# Restore NuGet packages
echo -e "${YELLOW}Restoring NuGet packages...${NC}"
dotnet restore
check_result "Restore"
echo ""

# Build the solution
echo -e "${YELLOW}Building solution...${NC}"
if [ "$VERBOSE" = true ]; then
    dotnet build --configuration Release --no-restore
else
    dotnet build --configuration Release --no-restore --verbosity minimal
fi
check_result "Build"
echo ""

# Run tests
echo -e "${YELLOW}Running tests...${NC}"

TEST_ARGS=""

# Add filter for skipping integration tests if requested
if [ "$SKIP_INTEGRATION" = true ]; then
    TEST_ARGS="$TEST_ARGS --filter Category!=Integration"
    echo "  (Skipping integration tests)"
fi

# Add code coverage if requested
if [ "$COVERAGE" = true ]; then
    TEST_ARGS="$TEST_ARGS --collect:\"XPlat Code Coverage\" --results-directory TestResults"
    echo "  (Collecting code coverage)"
fi

# Add verbosity
if [ "$VERBOSE" = true ]; then
    TEST_ARGS="$TEST_ARGS --logger console;verbosity=detailed"
else
    TEST_ARGS="$TEST_ARGS --logger console;verbosity=normal"
fi

# Execute tests
dotnet test $TEST_ARGS
check_result "Tests"
echo ""

# Generate coverage report if coverage was collected
if [ "$COVERAGE" = true ]; then
    echo -e "${YELLOW}Generating coverage report...${NC}"
    COVERAGE_FILE=$(find TestResults -name "coverage.cobertura.xml" -type f | head -n 1)
    if [ -n "$COVERAGE_FILE" ]; then
        echo "Coverage file found: $COVERAGE_FILE"
        # You can use ReportGenerator here if installed
        # reportgenerator -reports:$COVERAGE_FILE -targetdir:TestResults/CoverageReport -reporttypes:Html
    fi
fi

echo ""
echo -e "${GREEN}Build and test completed successfully!${NC}"
echo ""

# Show summary
echo -e "${CYAN}Summary:${NC}"
echo -e "${CYAN}--------${NC}"

# Count test projects
TEST_PROJECTS=$(find . -name "*.Tests.csproj" | wc -l)
echo "Test Projects: $TEST_PROJECTS"

# Show build output location
echo "Build Output: bin/Release/net8.0/"

if [ "$COVERAGE" = true ]; then
    echo "Coverage Results: TestResults/"
fi

echo ""
echo -e "${CYAN}Additional test options:${NC}"
echo "  Run only unit tests:        ./build-and-test.sh --skip-integration"
echo "  Run with code coverage:     ./build-and-test.sh --coverage"
echo "  Run with verbose output:    ./build-and-test.sh --verbose"
echo "  Run specific test:          dotnet test --filter \"FullyQualifiedName~ImportCommand\""
echo ""