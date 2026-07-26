# ADR 0001: MVP 애플리케이션 구조 및 개발 방향

## Status

Proposed

## Context

프로젝트는 Blazor Web App 기반으로 빠르게 MVP를 검증해야 하며, 이후 Azure 배포/운영 단계로 점진 확장할 계획이다.  
동시에 OCR/LLM/검색/큐 같은 외부 의존은 향후 교체 가능해야 하며, 초기에는 특정 공급자에 강결합되면 유지보수 비용이 커진다.

## Decision

다음 구조와 개발 원칙을 채택한다.

### 1) 계층 구조(유지)

1. `StudyRange.Web`  
   - Blazor UI, 사용자 흐름, 화면 상태 관리  
   - 비즈니스 규칙 직접 구현 금지
2. `StudyRange.Application`  
   - 유스케이스 오케스트레이션  
   - 인터페이스(Port) 기반으로 Infrastructure 의존 분리
3. `StudyRange.Domain`  
   - 핵심 엔티티/값 객체/도메인 규칙
4. `StudyRange.Infrastructure`  
   - 저장소, 파일저장, 비동기 처리, 외부 서비스 어댑터

의존 방향은 **Web → Application → Domain** 이며, Infrastructure는 Application/Domain 계약을 구현하는 플러그인으로 둔다.

### 2) MVP 범위(LLM 제외)

MVP에서 구현할 기능은 다음으로 제한한다.

- 워크스페이스 생성/조회
- 시험 범위 입력/검증
- 문서 업로드(교과서 PDF, 학습지, 손글씨 노트)
- 처리 상태 조회(Queued/Processing/Completed/Failed)
- 시험범위-콘텐츠 준비 상태 확인

다음은 MVP에서 제외한다.

- LLM 기반 요약/퀴즈/플래시카드 생성
- 고급 추천/개인화
- 교사/학부모 전용 기능

### 3) 교체 가능성(Swapability) 원칙

- OCR, LLM, 검색, 큐, 저장소는 모두 인터페이스 경유
- UI/유스케이스는 구체 SDK/벤더 타입을 참조하지 않음
- 환경별(Local/Dev/Prod) 구성 값으로 구현체 전환 가능하게 설계
- 공급자 선택 전 기능에는 `SPIKE REQUIRED` 명시

### 4) 개발 단계 전략

1. **Stage A (MVP Core)**  
   도메인/유스케이스/기본 UI 플로우 완성
2. **Stage B (Infra Hardening)**  
   PostgreSQL, 실제 오브젝트 스토리지, 운영 로그/모니터링 연결
3. **Stage C (AI Enablement)**  
   스파이크 결과로 LLM/OCR 공급자 확정 후 어댑터 추가
4. **Stage D (Production Readiness)**  
   보안, 비용 통제, 성능 튜닝, 운영 런북 정리

### 5) 코드베이스 운영 원칙

- 기능 단위(Workspaces/ExamRanges/Documents)로 수직 분할
- 외부 연동 오류는 숨기지 않고 상태/메시지로 명시
- 비동기 작업은 상태 전이 추적 가능하게 이벤트/로그 중심으로 설계
- ADR/Spike 문서 선행 후 구현 진행

## Consequences

### Positive

- 기능 확장 전에도 구조 안정성이 확보된다.
- 외부 AI/OCR 공급자 변경 비용을 낮출 수 있다.
- MVP 속도와 장기 유지보수성을 동시에 가져갈 수 있다.

### Negative

- 초기 구현 대비 설계/문서화 비용이 증가한다.
- 인터페이스 계층으로 인한 보일러플레이트가 늘어난다.
- 스파이크 완료 전까지 일부 기능은 Placeholder 상태로 남는다.

## Alternatives Considered

1. 단일 Blazor 프로젝트에 모든 로직 집중  
   - 초기 속도는 빠르나, 공급자 교체/테스트/확장에 취약하여 제외
2. 초기부터 마이크로서비스 분리  
   - MVP 단계에서 과도한 복잡도/운영비용 발생으로 제외
3. LLM 우선 통합 후 구조 정리  
   - 벤더 종속/재작업 리스크가 높아 제외

## Related Spikes

- SPIKE REQUIRED: OCR/손글씨 인식 공급자 비교(한국어 정확도, 비용, 처리량)
- SPIKE REQUIRED: LLM 후보 비교(Azure 모델 vs Local 모델, 요약/문제생성 품질)
- SPIKE REQUIRED: 비동기 처리 인프라(큐/워커) 운영 모델
- SPIKE REQUIRED: 검색/인덱싱 전략(PostgreSQL 중심 vs 외부 검색엔진)
