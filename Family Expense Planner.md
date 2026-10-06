**Goal:**

	The goal of the project is to create a space for individuals or families to keep track of their monthly spending in order to keep their finances in a positive place.
	
**Stacks:**

	Frontend: Mudblazor
	Backend: C# 
	Database: MySql
	Authentication: Third Party (Google or Auth 0) ()

**Time Break Down:**

	Frontend: 
		Login Page (6hrs)
		Setup Page (6hrs)
		Household Dashboard (8hrs)
		Income Page (8hrs)
		Bill Page (8hrs)
		Profile Page (6hrs)
		Reset Password Page (6hrs)
		
	Backend:
		User (Entity) (Service) (Repository) (Controller) (10hr)
		Household (Entity) (Service) (Repository) (Controller) (10hrs)
		Income (Entity) (Service) (Controller) (8hrs)
		Bills (Entity) (Service) (Controller) (8hrs)
		Google Authentication (Service) (10hrs)

Users
```
Id - UID
Name - String
Email - String
Password - HashString
```

Household
```
Id - UID
Name - String
Incomes - Double
Payments - Double
```

Income
```
Id - UID
Name - String
Value - Double
StartDate - DateTime
EndDate - DateTime
Recurring - Boolean
Interval - Enum
UserId - UID
```

Bills
```
Id - UID
Name - String
Value - Double
StartDate - DateTime
EndDate - DateTime
Recurring - Boolean
Interval - Enum
Household - UID
UserId - UID
```

Items
```
Id - UID
Name - String
Description - String || null
Price - Double
HouseholdId - UID
```

User Household
```
Id - UID
UserId - UID
HouseholdId - UID
```

Goals
```
Id - UID
Name - String
Value - Double
StartDate - DateTime
EndDate - DateTime
Recurring - Boolean
Interval - Enum
Household - UID
UserId - UID

-FROM BILL IMPLEMENTATION-

Total - Double  
```

**Feature List (Future Additions):**

- Being able to merge households
- Being able to add itemized goals
- Mobile version 
- Calendar Deadlines
- Item Ingestion
- Amazon Linking
 
**Error Handling:**

- Create error Enums to throw an Exception with
- After the Enum should follow the Id or number of significance that was trying to be reached

**Finish Line Requirement:**

	The finish line for this project would be being able to log into a household account and add income to the house hold with labels. Then the user should be able to then add in expenses to be subtracted from the total income of the household. Whatever is left should be able to be freely allocated to either savings or fun money. You should be able to then input where the family is right now financially and calculate how much it would take to reach saving goals.

	
