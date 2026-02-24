# API Breaking Change Report

- 🛑 **BREAKING**: GET /users response field 'id' changed type from string to integer
- 🛑 **BREAKING**: GET /users response removed field 'name'
- 🛑 **BREAKING**: POST /users request added required field 'name'
- 🛑 **BREAKING**: POST /users request added required field 'email'
- 🛑 **BREAKING**: POST /users request field 'age' changed type from string to integer
- 🛑 **BREAKING**: POST /users request field 'status' removed enum value 'inactive'
- 🛑 **BREAKING**: DELETE /users/{id} removed
