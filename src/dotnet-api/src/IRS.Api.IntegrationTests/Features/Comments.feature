Feature: Section Comments
    As a team member
    I want to comment on a section
    So that collaboration is captured in context

Background:
    Given the database is clean

Scenario: Add a comment and retrieve comments
    Given I am logged in as a user with email "analyst@example.com"
    And I have created a team named "Alpha Fund"
    And I create a research page for team "Alpha Fund" with security:
        | figi         | ticker | name       | security_type |
        | BBG000B9XRY4 | AAPL   | Apple Inc. | Corporate     |
    And I select the first section of the created research page
    When I add a comment "Great ESG improvements recently"
    Then the response status code should be 201
    When I list comments for the selected section
    Then the response status code should be 200
    And the comments list should include "Great ESG improvements recently"
