FROM node:22-alpine AS web
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS api
WORKDIR /src
COPY backend/ backend/
COPY --from=web /src/backend/src/StudyHub.Api/wwwroot backend/src/StudyHub.Api/wwwroot
RUN dotnet publish backend/src/StudyHub.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=api /app ./
COPY data/ /data/seed/
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Default="Data Source=/data/db/studyhub.db" \
    Seed__BankPath=/data/seed/question-banks/AWS_reStart_102_preguntas.json
RUN mkdir -p /data/db
VOLUME /data/db
EXPOSE 8080
ENTRYPOINT ["dotnet", "StudyHub.Api.dll"]
