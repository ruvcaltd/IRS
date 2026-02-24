Feature: Research Pages
    As a team member
    I want to create and view research pages
    So that my team can collaborate on securities

Background:
    Given the database is clean

Scenario: Create a research page and retrieve it
    Given I am logged in as a user with email "analyst@example.com"
    And I have created a team named "Alpha Fund"
    When I create a research page for team "Alpha Fund" with security:
        | figi     | ticker | name        | security_type |
        | BBG000B9XRY4 | AAPL   | Apple Inc. | Corporate     |
    Then the response status code should be 201
    And the research page should include 4 default sections
    When I get the created research page
    Then the response status code should be 200
    And the research page should include 4 default sections

Scenario: Delete a research page and cascade-remove agents, runs, sections and comments
    Given I am logged in as a user with email "analyst@example.com"
    And I have created a team named "Alpha Fund"
    And an agent exists for team "Alpha Fund" with name "QA Agent" and visibility "Team"
    And I create a research page for team "Alpha Fund" with security:
        | figi         | ticker | name       | security_type |
        | BBG000B9XRY4 | AAPL   | Apple Inc. | Corporate     |
    When I attach agent "QA Agent" to the created research page
    And I attach agent "QA Agent" to the first section of the created research page
    And an agent run exists for the page agent on the first section
    And an agent run exists for the section agent on the first section
    Given I select the first section of the created research page
    When I add a comment "test comment"
    When I delete the research page
    Then the response status code should be 204
    When I get the created research page
    Then the response status code should be 404
