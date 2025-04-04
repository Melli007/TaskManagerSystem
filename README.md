# Task Manager System
Task Manager System is a .NET-based application consisting of two projects:

- Backend (API): An ASP.NET Core RESTful API that handles task operations and real-time communication using SignalR.

- Frontend (MVC Client): An ASP.NET Core MVC application providing a user-friendly interface for task management.

#### Note: Originally developed as a school project, this system earned a perfect grade upon presentation. However, it never fully realized my vision of a perfect Employee Task Manager System. I eventually left the project behind to focus on other endeavors, so it hasn't seen active development for some time. With further refinement and enhancements, it still holds the potential to be an outstanding solution.
#### Additional Note on Image Paths  
On my laptop, the project runs perfectly—every endpoint functions as expected still. If you follow the setup instructions, it should work seamlessly on your system as well. However, please note that the image paths for user picture posting and getting are configured to store files on the local server. You might need to adjust these paths or configure them to point to a cloud storage solution if you prefer, as the current setup saves images locally rather than on a platform like Cloudinary or similar.
## Screenshots of the App

For a full gallery of screenshots showcasing the various features of the app, please refer to the [Screenshots of the Employee task manager folder].

* Highlights include:
  - Login Screen
  - Dashboard Overview
  - Task Management Details
  - Real-time updates with SignalR

# Table of Contents



- Getting Started

    - Prerequisites

    - Setup Instructions

- Configuration & Database Setup

- Running the Application

- User Account Creation

- Future Enhancements

- Contributing

# Getting Started
## Prerequisites
- Visual Studio 2022 (or later) with the .NET development workload installed.

- .NET 6/7 SDK (or the version specified in the solution).

- SQL Server (or another supported database engine) installed locally or accessible remotely.

## Setup Instructions
### 1. Clone the Repository  
Open your terminal and execute the following commands:

1. mkdir TaskManager  
2. cd TaskManager    
3. git clone https://github.com/Melli007/TaskManagerSystem.git
         
After cloning, open Visual Studio, click "Open a project or solution", navigate to the folder you just created, then go to the Backend folder and open the (EtechTaskManagerBackend.sln) solution.

# Configuration & Database Setup
### 2. Configure the Backend  
 - In the Backend project, locate the appsettings.json file.
 - Update the ConnectionStrings section with your own connection string. For example:  

        {      
            "ConnectionStrings": {  
              "DefaultConnection": "Server=YOUR_SERVER;Database=YOUR_DATABASE;User                                      Id=YOUR_USER;Password=YOUR_PASSWORD;"  
            },  
            // ... other settings  
          }          
### 3. Run Database Migrations  
 1. Open the Package Manager Console (PMC) in Visual Studio.

 2. Ensure the Default Project in PMC is set to the Backend project.

 3. Create and apply the migrations to set up your database:

- To create a new migration:  
    #### Add-Migration InitialCreate  
- To apply the migration and update the database:  
    #### Update-Database
 - This process creates the necessary tables in your database as defined in your entity models.  

# Running the Application
### 4. Multi-Run the Solution  
1. In Visual Studio, click the dropdown arrow next to the green Play button (labeled Start).

2. Select Configure Startup Projects....

3. Choose Multiple Startup Projects.

4. Set both the Backend and Frontend (MVC Client) projects to Start.

5. Click Apply and OK.

6. Run the solution using F5 or Ctrl+F5.

# User Account Creation
### 5. Create User Accounts  
1. Once the Backend is running, navigate to its Swagger UI.
2. Use the Swagger endpoints to create new user accounts.

3. To create a user, use the endpoint /api/Users/CreateUsers with a JSON request body. For example:

- Creating an Admin:  
  ```bash
  {  
    "fullName": "Admin",  
    "username": "Admin",  
    "password": "YourPasswordHere",  
    "role": "Admin",  
    "email": "admin@example.com",  
    "phone": "1234567890",  
    "profession": "Administrator",  
    "createdAt": "2025-04-02T23:45:23.923Z",  
    "banned": false,  
    "isOnline": false,  
    "profilePicturePath": "path/to/profile/picture"  
  }  
- Creating a Normal User:  

  Change the "role" field to "Employee":

- Follow the prompts in Swagger to complete the user creation process and start testing the API endpoints.

# Future Enhancements
- Code Refactoring: Improve code organization and structure for better maintainability.

- UI/UX Improvements: Enhance the design of the MVC client for a more modern and intuitive user experience.

- Scalability Enhancements: Optimize the application architecture to support a larger user base and higher data volumes.

# Contributing
 Contributions are welcome! If you’d like to contribute, please follow these steps:

1. Fork the repository.

2. Create a new branch for your feature or bug fix.

3. Commit your changes with clear and descriptive commit messages.

4. Push your branch and open a Pull Request.
