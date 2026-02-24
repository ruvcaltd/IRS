Feature: User Authentication
    As a user of the IRS system
    I want to be able to register and login
    So that I can access my team's research

Background:
    Given the database is clean

Scenario: Successful user registration
    When I register a new user with email "test@example.com", password "Pass123!", and full name "John Doe"
    Then the response status code should be 201
    And the response should contain a user ID
    And the user should exist in the database

Scenario: User login with valid credentials
    Given a user exists with email "test@example.com" and password "Pass123!"
    When I login with email "test@example.com" and password "Pass123!"
    Then the response status code should be 200
    And the response should contain a JWT token
    And the JWT token should be valid

Scenario: User login with invalid credentials
    Given a user exists with email "test@example.com" and password "Pass123!"
    When I login with email "test@example.com" and password "WrongPass!"
    Then the response status code should be 401

Scenario: Duplicate email registration is rejected
    Given a user exists with email "test@example.com" and password "Pass123!"
    When I register a new user with email "test@example.com", password "Pass123!", and full name "Jane Doe"
    Then the response status code should be 400
