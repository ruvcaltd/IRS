Feature: My Research
    As a team member
    I want to list all research pages visible to me
    So I can open them quickly

Background:
    Given the database is clean

Scenario: List my research pages
    Given I am logged in as a user with email "analyst@example.com"
    And I have created a team named "Alpha Fund"
    And I create a research page for team "Alpha Fund" with security:
        | figi         | ticker | name        | security_type |
        | BBG000B9XRY4 | AAPL   | Apple Inc.  | Corporate     |
    When I list my research pages
    Then the response status code should be 200
    And my research list should include the last created research page
