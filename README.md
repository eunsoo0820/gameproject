# drift

요구사항 명세서에 따른 Unity 6 1인칭 선박 생존 시제품.

Unity Hub에서 `Game` 폴더를 연다. 편집기 약관 및 패키지 임포트를 완료하면
`Assets/Drift/Scenes/Drift.unity`가 자동 생성된다.
`Drift > Create or Open Game` 메뉴로 장면을 연 뒤 Play를 누른다.

캐릭터 모델·선택과 실제 온라인 통신은 제외되어 있다.
현재 구현 범위와 조작은 [프로젝트 문서](Docs/ProjectBrief.md)를 참고한다.
도메인 자동 테스트는 `dotnet run --project Validation/DriftRules.csproj`로 실행한다.
