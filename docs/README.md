# CoffeeNChill – CLDV6212 Project

## Project overview

CoffeeNChill is a group project developed for CLDV6212. The solution uses Azure Functions and Azure Storage services to manage the application's data and functionality.

## Team contributions

### Part 1 – Neha

Neha completed the 1st part of part 1 of the project and implemented the menu-item functionality used by the application.

- Created the menu-item data model
- Implemented the menu-item storage service
- Implemented the functions for creating, retrieving, updating, and deleting menu items
- Added support for retrieving menu items by category
- Connected the menu functionality to Azure Table Storage
- No automated tests were included for this part

### Part 2 – Tahir

Tahir completed 2nd part of part 1, which covers the document functionality.

- Document upload functionality
- Document listing and retrieval functionality
- Document download functionality
- Tests were created for the document endpoints

### Part 3 – Douglass

Douglass completed 3rd part of part 1 and served as the group leader for the project.

- Coordinated the team's work and helped ensure that the different project sections worked together
- Identified and resolved implementation, configuration, build, and integration issues encountered by the group
- Assisted team members with troubleshooting where required
- Verified the menu-item functionality
- Created Postman tests for the menu-item endpoints
- Helped configure Azure Functions, Azurite, and the local testing environment

## Testing

Postman collections are used to test the HTTP endpoints. Before running the tests:

1. Start Docker Desktop.
2. Start the Azurite container.
3. Run the Azure Functions project.
4. Update the Postman `baseUrl` collection variable to match the port shown by Rider, for example:

   ```text
   http://localhost:7055/api
   ```

5. Run the appropriate Postman collection.
### Testing Results
<img width="963" height="455" alt="Screenshot 2026-09-12 at 15 46 15" src="https://github.com/user-attachments/assets/25f09806-babb-44fb-a1cf-f3efd047f206" />


## Technologies

- C# and .NET 8
- Azure Functions
- Azure Storage
- Azurite
- Docker
- JetBrains Rider
- Postman
