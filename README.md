# POE-PROG6212-part-1

RaceDay — Part 1: System Planning and Database
Description

RaceDay is a full-stack event management system built for the South African road running, walking, and cycling community. It allows Event Organisers to create and manage events, categories, and participant results, while Participants can browse upcoming events, enter events, track their personal performance history, and prepare for race day using route information.

This repository covers Part 1 of the project: system planning. It contains the Entity Relationship Diagram, the API endpoint plan, and the SQL database script for the RaceDay system. No application code is written in this part.

Roles

The system supports two distinct user roles:

Organiser — can create, edit, and delete events, manage event categories, capture participant results, and view all event enrolments.
Participant — can create an account, browse events, enter an event by selecting a category, view their own enrolments, and track their personal results.
Repository Structure
/docs
  ├── ERD.png                    # Entity Relationship Diagram
  ├── api-endpoint-plan.md       # Full API endpoint plan
  └── raceday-database.sql       # SQL script (schema + seed data)
/.github/workflows
  └── ci.yml                     # GitHub Actions workflow (validates /docs structure)
README.md
Setup Instructions
Clone this repository:
bash
   git clone <YOUR_REPO_URL>
Open docs/raceday-database.sql in SQL Server Management Studio (SSMS).
Run the entire script (F5) against a local SQL Server / LocalDB instance. The script drops and recreates RaceDayDB from scratch, so it runs cleanly on any instance.
Review the ERD in docs/ERD.png and the endpoint plan in docs/api-endpoint-plan.md.
CI/CD

GitHub Actions validates that the /docs folder exists and contains the required files on every push.

Screenshot of a successful green build:

![CI/CD Success](docs/ci-success.PNG)

Video Presentation

An unlisted YouTube video walking through the planning documents, the ERD decisions, the endpoint plan choices, and a live run of the SQL script in SSMS:

[Insert your YouTube link here]
