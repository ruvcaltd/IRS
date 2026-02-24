Feature: Team Management
    As an investment team member
    I want to create teams, join existing teams, and manage team membership
    So that I can collaborate securely with my colleagues on research

Background:
    Given the database is clean

# User Story 1: Team Creation
Scenario: User creates a new team after registration
    Given I am logged in as a user with email "alice@alphafund.com"
    When I create a team named "Alpha Fund"
    Then the response status code should be 201
    And the response should contain a team with name "Alpha Fund"
    And the response should indicate the user has role "Admin"
    And the user should be the only member of the team

Scenario: Team names must be unique
    Given I am logged in as a user with email "alice@alphafund.com"
    And I have created a team named "Alpha Fund"
    When I try to create another team named "Alpha Fund"
    Then the response status code should be 400
    And the response should contain error message "already exists"

# User Story 2: Team Joining (Requesting Access)
Scenario: User can search for teams by name
    Given a team named "Alpha Fund" exists
    And I am logged in as a user with email "charlie@example.com"
    When I search for teams with query "Alpha"
    Then the response status code should be 200
    And the search results should include a team named "Alpha Fund"

Scenario: User requests to join an existing team
    Given a team named "Alpha Fund" exists
    And I am logged in as a user with email "charlie@example.com"
    When I request to join the team "Alpha Fund"
    Then the response status code should be 202
    And my membership status should be "PENDING"
    And the response should indicate "Request sent to admins"

Scenario: User cannot join the same team twice
    Given a team named "Alpha Fund" exists
    And I am logged in as a user with email "charlie@example.com"
    And I have requested to join team "Alpha Fund"
    When I request to join the team "Alpha Fund" again
    Then the response status code should be 400
    And the response should contain error message "already a member"

# User Story 3: Admin Approval Workflow
Scenario: Admin can view pending membership requests
    Given a team named "Alpha Fund" exists, created by "alice@alphafund.com"
    And "charlie@example.com" has requested to join the team
    And I am logged in as "alice@alphafund.com" (the team admin)
    When I retrieve pending requests for the team
    Then the response status code should be 200
    And the pending requests should include user "charlie@example.com"
    And there should be 1 pending request

Scenario: Admin approves a user with Contributor role
    Given a team named "Alpha Fund" exists, created by "alice@alphafund.com"
    And "charlie@example.com" has requested to join the team
    And I am logged in as "alice@alphafund.com" (the team admin)
    When I approve "charlie@example.com" for the team with role "Contributor"
    Then the response status code should be 200
    And the response should indicate role "Contributor"
    And "charlie@example.com" should have status "ACTIVE" in the team
    And "charlie@example.com" should be able to access the team

Scenario: Admin rejects a membership request
    Given a team named "Alpha Fund" exists, created by "alice@alphafund.com"
    And "charlie@example.com" has requested to join the team
    And I am logged in as "alice@alphafund.com" (the team admin)
    When I reject the request from "charlie@example.com"
    Then the response status code should be 204
    And "charlie@example.com" should not be a member of the team

Scenario: Non-admin cannot approve membership requests
    Given a team named "Alpha Fund" exists, created by "alice@alphafund.com"
    And "bob@example.com" is a "Contributor" member of the team
    And "charlie@example.com" has requested to join the team
    And I am logged in as "bob@example.com" (non-admin)
    When I try to approve "charlie@example.com" for the team
    Then the response status code should be 403

Scenario: Team member can view team member list
    Given a team named "Alpha Fund" exists, created by "alice@alphafund.com"
    And "bob@example.com" is a "Contributor" member of the team
    And "charlie@example.com" is a "ReadOnly" member of the team
    And I am logged in as "bob@example.com"
    When I retrieve the members of the team
    Then the response status code should be 200
    And the member list should include "alice@alphafund.com" as "Admin"
    And the member list should include "bob@example.com" as "Contributor"
    And the member list should include "charlie@example.com" as "ReadOnly"

Scenario: Logged-in user can view their own teams
    Given I am logged in as a user with email "alice@alphafund.com"
    And I have created a team named "Alpha Fund"
    When I retrieve my teams
    Then the response status code should be 200
    And the team list should include a team named "Alpha Fund" with role "Admin"
