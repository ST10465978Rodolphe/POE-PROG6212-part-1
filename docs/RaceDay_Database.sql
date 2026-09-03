IF DB_ID('RaceDayDB') IS NOT NULL
BEGIN
    ALTER DATABASE RaceDayDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE RaceDayDB;
END
GO

CREATE DATABASE RaceDayDB;
GO

USE RaceDayDB;
GO

-- Stores all Organiser and Participant accounts
CREATE TABLE [User]
(
    UserID INT IDENTITY(1,1) PRIMARY KEY,
    FirstName VARCHAR(50) NOT NULL,
    LastName VARCHAR(50) NOT NULL,
    Email VARCHAR(100) NOT NULL UNIQUE,
    Password VARCHAR(255) NOT NULL,
    Role VARCHAR(20) NOT NULL,
    Phone VARCHAR(20),

    CONSTRAINT CK_User_Role
        CHECK (Role IN ('Organiser', 'Participant'))
);
GO

-- Stores events created by Organisers
CREATE TABLE Event
(
    EventID INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserID INT NOT NULL,
    EventName VARCHAR(100) NOT NULL,
    Description VARCHAR(255),
    EventDate DATE NOT NULL,
    Location VARCHAR(150) NOT NULL,
    EventType VARCHAR(30) NOT NULL,

    CONSTRAINT FK_Event_Organiser
        FOREIGN KEY (OrganiserID)
        REFERENCES [User](UserID)
);
GO

-- Stores race categories within an event (e.g. 5km, 10km, 21km)
CREATE TABLE Category
(
    CategoryID INT IDENTITY(1,1) PRIMARY KEY,
    EventID INT NOT NULL,
    CategoryName VARCHAR(100) NOT NULL,
    Description VARCHAR(255),
    Distance DECIMAL(6,2) NOT NULL,
    EntryFee DECIMAL(10,2) NOT NULL DEFAULT 0,

    CONSTRAINT FK_Category_Event
        FOREIGN KEY (EventID)
        REFERENCES Event(EventID),

    CONSTRAINT CK_Category_Distance
        CHECK (Distance > 0),

    CONSTRAINT CK_Category_EntryFee
        CHECK (EntryFee >= 0),

    CONSTRAINT UQ_Category_Event_Name
        UNIQUE (EventID, CategoryName)
);
GO

-- Links a Participant to a Category they've entered
CREATE TABLE Enrolment
(
    EnrolmentID INT IDENTITY(1,1) PRIMARY KEY,
    ParticipantID INT NOT NULL,
    CategoryID INT NOT NULL,
    EnrolmentDate DATE NOT NULL DEFAULT GETDATE(),
    Status VARCHAR(20) NOT NULL DEFAULT 'Confirmed',

    CONSTRAINT FK_Enrolment_Participant
        FOREIGN KEY (ParticipantID)
        REFERENCES [User](UserID),

    CONSTRAINT FK_Enrolment_Category
        FOREIGN KEY (CategoryID)
        REFERENCES Category(CategoryID),

    CONSTRAINT CK_Enrolment_Status
        CHECK (Status IN ('Confirmed', 'Cancelled')),

    CONSTRAINT UQ_Participant_Category
        UNIQUE (ParticipantID, CategoryID)
);
GO

-- Stores race results once captured by an Organiser
CREATE TABLE Result
(
    ResultID INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentID INT NOT NULL UNIQUE,
    FinishTime TIME NOT NULL,
    FinishingPosition INT NOT NULL,
    ResultDate DATE NOT NULL DEFAULT GETDATE(),

    CONSTRAINT FK_Result_Enrolment
        FOREIGN KEY (EnrolmentID)
        REFERENCES Enrolment(EnrolmentID),

    CONSTRAINT CK_Result_Position
        CHECK (FinishingPosition > 0)
);
GO

-- Stores route details for an event (start/finish points, distance)
CREATE TABLE Route
(
    RouteID INT IDENTITY(1,1) PRIMARY KEY,
    EventID INT NOT NULL UNIQUE,
    RouteName VARCHAR(100) NOT NULL,
    Description VARCHAR(255),
    StartLocation VARCHAR(150) NOT NULL,
    FinishLocation VARCHAR(150) NOT NULL,
    Distance DECIMAL(6,2) NOT NULL,

    CONSTRAINT FK_Route_Event
        FOREIGN KEY (EventID)
        REFERENCES Event(EventID),

    CONSTRAINT CK_Route_Distance
        CHECK (Distance > 0)
);
GO

-- Organisers

INSERT INTO [User]
    (FirstName, LastName, Email, Password, Role, Phone)
VALUES
    ('Thabo', 'Mokoena', 'thabo.mokoena@raceday.co.za',
     'Password123', 'Organiser', '0712345678'),

    ('Lerato', 'Dlamini', 'lerato.dlamini@raceday.co.za',
     'Password123', 'Organiser', '0723456789');
GO

-- Participants

INSERT INTO [User]
    (FirstName, LastName, Email, Password, Role, Phone)
VALUES
    ('Daniel', 'Williams', 'daniel.williams@email.com',
     'Password123', 'Participant', '0734567890'),

    ('Siyanda', 'Nkosi', 'siyanda.nkosi@email.com',
     'Password123', 'Participant', '0745678901');
GO

-- Events

INSERT INTO Event
    (OrganiserID, EventName, Description, EventDate,
     Location, EventType)
VALUES
    (1, 'Cape Town Road Run',
     'Community road running event in Cape Town',
     '2026-10-18', 'Cape Town', 'Running'),

    (1, 'Table Mountain Charity Walk',
     'Charity walking event for the local community',
     '2026-11-08', 'Cape Town', 'Walking'),

    (2, 'Cape Cycle Challenge',
     'Road cycling event for different experience levels',
     '2026-11-22', 'Cape Town', 'Cycling');
GO

-- Categories

INSERT INTO Category
    (EventID, CategoryName, Description, Distance, EntryFee)
VALUES
    (1, '10 KM Open',
     '10 kilometre open category', 10.00, 150.00),

    (1, '10 KM Junior',
     '10 kilometre junior category', 10.00, 100.00),

    (2, '8 KM Open Walk',
     '8 kilometre community walk', 8.00, 80.00),

    (2, '8 KM Family Walk',
     '8 kilometre family walking category', 8.00, 120.00),

    (3, '40 KM Open',
     '40 kilometre open cycling category', 40.00, 250.00),

    (3, '40 KM Development',
     '40 kilometre development cycling category', 40.00, 180.00);
GO

-- Enrolments

INSERT INTO Enrolment
    (ParticipantID, CategoryID, Status)
VALUES
    (3, 1, 'Confirmed'),

    (4, 2, 'Confirmed'),

    (3, 3, 'Confirmed'),

    (4, 5, 'Confirmed');
GO

-- Results

INSERT INTO Result
    (EnrolmentID, FinishTime, FinishingPosition)
VALUES
    (1, '00:52:35', 12),

    (2, '01:04:20', 25);
GO

-- Routes

INSERT INTO Route
    (EventID, RouteName, Description,
     StartLocation, FinishLocation, Distance)
VALUES
    (1, 'Cape Town City Route',
     'Road route through selected areas of Cape Town',
     'Company Gardens', 'Green Point', 10.00),

    (2, 'Table Mountain Community Route',
     'Walking route around the Table Mountain area',
     'Platteklip Road', 'Signal Hill', 8.00),

    (3, 'Cape Cycling Route',
     'Road cycling route around Cape Town',
     'Cape Town Stadium', 'Camps Bay', 40.00);
GO

SELECT * FROM [User];
SELECT * FROM Event;
SELECT * FROM Category;
SELECT * FROM Enrolment;
SELECT * FROM Result;
SELECT * FROM Route;
GO
