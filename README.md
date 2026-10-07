# drift

요구사항 명세서에 따른 Unity 6 1인칭 선박 생존 시제품.

Unity Hub에서 `Game` 폴더를 연다. 시작 장면은
`Assets/Drift/Scenes/Drift.unity`다.
`Drift > Create or Open Game` 메뉴로 장면을 연 뒤 Play를 누른다.

로컬 Windows 개발 빌드: `Builds/Windows-20261006/drift.exe`.
실행 파일을 같은 폴더의 데이터·DLL 파일과 함께 유지한다.
게임에서 **항해 → 방 만들기 → 항해 시작**을 선택한다.

캐릭터 모델·선택과 실제 온라인 통신은 제외되어 있다.
현재 구현 범위와 조작은 [프로젝트 문서](Docs/ProjectBrief.md)를 참고한다.
도메인 자동 테스트는 `dotnet run --project Validation/DriftRules.csproj`로 실행한다.
