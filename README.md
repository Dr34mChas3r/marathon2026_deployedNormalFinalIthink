# Green Tasty — restaurant reservations and pre-orders

Green Tasty is a restaurant web application for managing reservations, waiter service, and customer pre-orders. It includes a kitchen display workflow and group pre-orders where guests can join a shared reservation.

## What is included

- **Customer experience:** browse restaurants and dishes, book a table, manage reservations, and create pre-orders.
- **Waiter workspace:** create and manage reservations for the assigned restaurant, add pre-orders, and update service status.
- **Kitchen Display System:** move submitted orders through preparation, ready, and served states.
- **Group pre-orders:** guests join a reservation, choose dishes, and the owner reviews the combined order.
- **Restaurant operations:** table availability, feedback, notifications, and reporting.

## Project layout

| Path | Description |
| --- | --- |
| `frontend/restaurant-frontend-app/` | Angular single-page application |
| `backend/restaurant-backend-app/dnapp/lambdas/api-handler/` | .NET API Gateway Lambda and application services |
| `backend/restaurant-backend-app/Tests/` | Backend unit and handler tests |
| `homework-*.md`, `homework-design/` | Workshop assignments and design material |
| `done-homeworks.md` | Completed homework record |

The backend uses AWS API Gateway, Lambda, DynamoDB, Cognito, S3, and SQS. The frontend and backend are deployed separately.

## Requirements

- Node.js `22.22.3+` (22.x) or `24.15.0+`, with npm
- .NET SDK 10
- Python 3 and a virtual environment for AWS Syndicate deployments
- AWS CLI credentials only when deploying to AWS

## Run the frontend locally

```powershell
Set-Location .\frontend\restaurant-frontend-app
npm ci
npm start
```

Open `http://localhost:4200`. The development environment's API URL is a placeholder: set your API Gateway URL in `src/environments/environment.development.ts` before using real backend endpoints. The group-order demo can be opened with `?demo` and uses local mock flows.

To create a production build, provide the backend URL through the environment rather than editing generated environment files:

```powershell
$env:BACKEND_URL = "https://YOUR-API-ID.execute-api.YOUR-REGION.amazonaws.com/develop"
npm run build -- --configuration production
```

The build script generates `src/environments/environment.production.ts`; this generated file is ignored by Git.

## Run tests

Frontend:

```powershell
Set-Location .\frontend\restaurant-frontend-app
npm test -- --watch=false
npm run test:hw
npx tsc -p tsconfig.app.json --noEmit
npx tsc -p tsconfig.spec.json --noEmit
```

Backend:

```powershell
Set-Location .\backend\restaurant-backend-app
dotnet test .\Tests\APIHandler.Tests.csproj
```

## Backend setup and deployment

The backend deployment uses AWS Syndicate. Configure an AWS CLI profile with permissions appropriate for your environment; prefer AWS IAM Identity Center/SSO or short-lived credentials over long-lived access keys.

To create an IAM Identity Center profile, run `aws configure sso --profile restaurant-dev` and complete the prompts.

From `backend/restaurant-backend-app` in PowerShell:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install aws-syndicate
$env:AWS_PROFILE = "restaurant-dev"
$env:SDCT_CONF = (Resolve-Path ".\.syndicate-config-restaurant-backend-app").Path
dotnet build .\restaurant-backend-app.sln
syndicate build
syndicate deploy
```

Review the Syndicate configuration and resource names for your AWS account before deploying. See [`backend/restaurant-backend-app/local-setup-guide.md`](backend/restaurant-backend-app/local-setup-guide.md) for additional setup details and [`backend/restaurant-backend-app/docs/ApiRoutes.md`](backend/restaurant-backend-app/docs/ApiRoutes.md) for API routes.

## Security and configuration

- Never commit AWS access keys, passwords, private keys, tokens, or populated `.env` files.
- Store local credentials outside the repository (for example, in an AWS CLI profile); use your deployment platform's secret store for CI.
- Keep secrets out of Angular configuration: values compiled into a browser bundle are public.
- `.env`, `.env.*` (except `.env.example`), AWS credential directories, and common private-key/certificate files are ignored by Git. Verify `git status` before committing configuration changes.
- The tracked AWS resource identifiers and endpoint placeholders are configuration, not credentials. Replace environment-specific values when setting up your own deployment.

## Homework

See [`done-homeworks.md`](done-homeworks.md) for completed assignments. **Manual Testing is intentionally still pending**; it will be documented after the provided bug-report example is reviewed.
