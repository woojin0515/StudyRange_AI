# StudyRange AI

Blazor Server 기반 시험 범위 학습 코치 MVP입니다.

## 현재 구현 범위

- Workspace 생성/선택/삭제
- 시험 범위 등록
- 문서 업로드(파일 형식/시그니처/크기 검증)
- 비동기 문서 처리 큐/워커
  - PDF: 페이지 추정 + 텍스트 샘플 요약
  - 이미지: 기본 요약 + (선택) Azure Vision OCR
  - 실패 작업 재처리 버튼 지원
  - Workspace 삭제 시 연결된 업로드 파일 정리
- AI 요약/퀴즈 생성
  - OpenAI API 키만으로 기본 동작
  - Endpoint + Deployment 설정 시 Azure OpenAI 경로 사용
  - 생성 결과 이력 저장(Workspace 단위)
  - 생성 이력 필터/불러오기/삭제 지원
- 교육 메타데이터 통합 조회
  - NCIC(기본 개정 교육과정)
  - KOTRY(API 키 기반 교과서 조회, 미설정 시 Mock 데이터)
  - NEIS(학교 정보 조회)
- 헬스체크(`/health`)
  - 설정 유효성(LLM/OCR 필수값)
  - 스토리지 쓰기 가능 여부
  - PostgreSQL 연결 상태

## 기술 스택

- .NET 9 / ASP.NET Core Blazor Server
- MudBlazor
- PostgreSQL (선택) / InMemory (개발 기본)

## 실행 방법

1. `src/StudyRange.Web/appsettings.Development.json` 설정
2. 실행

```bash
dotnet run --project src/StudyRange.Web/StudyRange.Web.csproj
```

## Copilot/Azure MCP 사전 준비

- 클라우드 코딩 에이전트 환경 고정을 위해 `.github/workflows/copilot-setup-steps.yml`를 추가했습니다.
- 기본적으로 .NET 9 SDK 설치/복원/빌드를 선행해, 화면 개편 작업 전 에이전트 실행 편차를 줄입니다.

## 주요 설정

### LLM

```json
"Llm": {
  "Provider": "OpenAI",
  "Model": "gpt-5-mini",
  "Endpoint": "",
  "ApiKey": "",
  "Deployment": ""
}
```

- `ApiKey` 필수
- `Endpoint`, `Deployment`를 함께 넣으면 Azure OpenAI 방식으로 호출
- 배포 환경에서는 아래 환경변수도 자동 인식  
  `OPENAI_API_KEY`, `AZURE_OPENAI_API_KEY`, `AZURE_OPENAI_KEY`, `OPENAI_KEY`, `LLM_API_KEY`,  
  `OPENAI_BASE_URL`, `AZURE_OPENAI_ENDPOINT`, `OPENAI_ENDPOINT`, `AZURE_OPENAI_BASE_URL`,  
  `AZURE_OPENAI_DEPLOYMENT`, `OPENAI_MODEL`, `AZURE_OPENAI_MODEL`, `LLM_MODEL`

- `/health`의 `entries.configuration.llmMissing`으로 누락된 LLM 설정 키를 바로 확인 가능

### 생성 동작(근거 기반)

- 요약/퀴즈 생성은 **교과서 PDF(TextbookPdf) 시험범위 페이지 텍스트**를 근거로만 생성합니다.
- Workspace에 교과서 PDF가 없으면 아래 순서로 자동 확보를 시도합니다.
  1) `EducationApis:TextbookPdf:CatalogDirectory`에서 `(개정년도)_(과목)_(출판사).pdf` 탐색
  2) `Provider = HttpTemplate`일 때 `UrlTemplate`로 원격 PDF 다운로드 후 카탈로그 캐시
- 선택 범위 페이지에서 근거 텍스트를 찾지 못하면 생성을 차단하고 오류를 반환합니다.
- 학교급/학년/출생년도/학교명/출판사/개정년도 메타데이터를 생성 프롬프트에 함께 반영합니다.

### Persistence

```json
"Persistence": {
  "Provider": "InMemory",
  "PostgreSqlConnectionString": ""
}
```

- `Provider = PostgreSql`일 때 `PostgreSqlConnectionString` 필수

### Education APIs

```json
"EducationApis": {
  "Kotry": {
    "BaseUrl": "http://www.kotry.kr",
    "ApiKey": ""
  },
  "Ncic": {
    "ApiKey": ""
  },
  "Neis": {
    "BaseUrl": "https://open.neis.go.kr",
    "ApiKey": ""
  },
  "TextbookPdf": {
    "Provider": "LocalCatalog",
    "CatalogDirectory": "App_Data/textbook-catalog",
    "UrlTemplate": "",
    "ApiKey": "",
    "ApiKeyHeaderName": "X-API-Key"
  }
}
```

- KOTRY는 OpenAPI 문서 기준으로 `f1`/`v1` 검색쌍이 필수이므로, 앱에서 `f1=keyword`, `v1=과목`, `schulGrad`, `pageUnit`, `pageIndex`를 자동으로 채워 호출합니다.
- KOTRY 조회는 `openTextBook.do`와 `book.do`를 순차 시도해 응답 포맷 차이를 흡수합니다.

- `Provider = LocalCatalog`: 로컬 카탈로그에서 `(개정년도)_(과목)_(출판사).pdf` 자동 탐색
- `Provider = HttpTemplate`: `UrlTemplate` 토큰(`{revision}`, `{subject}`, `{publisher}`, `{schoolLevel}`, `{grade}`, `{birthYear}`, `{schoolName}`)으로 원격 PDF 다운로드

### OCR (선택)

```json
"Ocr": {
  "Provider": "None",
  "Endpoint": "",
  "ApiKey": ""
}
```

- `Provider = AzureVision` + `Endpoint`, `ApiKey` 설정 시 이미지 OCR 수행
- 미설정 시 기존처럼 이미지 메타 요약만 생성