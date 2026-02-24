Feature: Page Agents
    As a team member
    I want to attach agents to research pages
    So that automated insights can run on our research

Background:
    Given the database is clean

Scenario: Attach, list, toggle, and view agent runs for a research page
    Given I am logged in as a user with email "analyst@example.com"
    And I have created a team named "Alpha Fund"
    And an agent exists for team "Alpha Fund" with name "QA Agent" and visibility "Team"
    And I create a research page for team "Alpha Fund" with security:
        | figi         | ticker | name       | security_type |
        | BBG000B9XRY4 | AAPL   | Apple Inc. | Corporate     |
    When I list available agents for team "Alpha Fund"
    Then the response status code should be 200
    And the available agents list should include "QA Agent"
    When I attach agent "QA Agent" to the created research page
    Then the response status code should be 201
    When I list agents for the created research page
    Then the response status code should be 200
    And the page agents list should include "QA Agent"
    When I disable the page agent for "QA Agent"
    Then the response status code should be 200
    And the page agent should be disabled
    And an agent run exists for the page agent on the first section
    When I list runs for the page agent
    Then the response status code should be 200
    And the agent runs should include status "Succeeded"

    When I run the page agent now
    Then the response status code should be 201
    When I list runs for the page agent
    Then the agent runs should include status "Succeeded"

Scenario: Create agent via API and confirm availability
    Given I am logged in as a user with email "analyst@example.com"
    And I have created a team named "Alpha Fund"
    When I create an agent for team "Alpha Fund" via API with name "API Agent" and visibility "Private"
    Then the response status code should be 201
    When I list available agents for team "Alpha Fund"
    Then the available agents list should include "API Agent"

Scenario: Update agent via API and confirm changes
    Given I am logged in as a user with email "analyst@example.com"
    And I have created a team named "Alpha Fund"
    When I create an agent for team "Alpha Fund" via API with name "API Agent" and visibility "Private"
    Then the response status code should be 201
    When I update agent "API Agent" for team "Alpha Fund" via API to name "API Agent Updated" and endpoint "https://api.changed.example/test"
    Then the response status code should be 200
    When I list available agents for team "Alpha Fund"
    Then the available agents list should include "API Agent Updated"

Scenario: Delete a page-level agent and its runs
    Given I am logged in as a user with email "analyst@example.com"
    And I have created a team named "Alpha Fund"
    And an agent exists for team "Alpha Fund" with name "QA Agent" and visibility "Team"
    And I create a research page for team "Alpha Fund" with security:
        | figi         | ticker | name       | security_type |
        | BBG000B9XRY4 | AAPL   | Apple Inc. | Corporate     |
    When I attach agent "QA Agent" to the created research page
    And an agent run exists for the page agent on the first section
    When I delete the page agent for "QA Agent"
    Then the response status code should be 204
    When I list agents for the created research page
    Then the page agents list should NOT include "QA Agent"
    When I list runs for the page agent
    Then the response status code should be 404

Scenario: Attach a section-level agent, delete it, and remove its runs
    Given I am logged in as a user with email "analyst@example.com"
    And I have created a team named "Alpha Fund"
    And an agent exists for team "Alpha Fund" with name "QA Agent" and visibility "Team"
    And I create a research page for team "Alpha Fund" with security:
        | figi         | ticker | name       | security_type |
        | BBG000B9XRY4 | AAPL   | Apple Inc. | Corporate     |
    When I attach agent "QA Agent" to the first section of the created research page
    And an agent run exists for the section agent on the first section
    When I delete the section agent for "QA Agent"
    Then the response status code should be 204
    When I list runs for the section agent
    Then the response status code should be 404