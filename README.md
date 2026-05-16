# osaHealth

A simple yet powerful health tracking service for you family

## Installation

1. Install pwsh
1. Install node
1. Install yarn
1. ```pwsh
   npm install
   npm install --global backlog
   npm run husky:init
   
   npm run backlog:init
   # npm run backlog
   # npm run backlog:ui

   npm run ai:init
   # Use the claude-mem runtime: Server

   # Copy and adjust the settings
   cp .env.example .env
   code .env
   ```

## Development

### Database

To access the database, use the provided Mongodb Express service
- <http://127.0.0.1:8081>
  - see [docker-compose.yml] for the right port
  - see [.env] for the credentials
                                                       
[docker-compose.yml]: ./docker-compose.yml
[.env]: ./.env