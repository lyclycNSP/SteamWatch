# SteamWatch WinUI PLAN

## Milestone 0 - Project Foundation
- [x] Create project directory at D:\000MyWorkSpace\SteamWatch.WinUI.
- [x] Initialize git repository.
- [x] Create solution with Core, Infrastructure, and Tests projects.
- [x] Establish SPEC.md, PLAN.md, and STATUS.md as the only project board.
- [ ] Install or enable WinUI 3 templates and Windows App SDK workload.

## Milestone 1 - Core Domain
- [x] Add limit period, limit scope, and enforcement mode domain types.
- [x] Add daily playtime records and Monday-based weekly aggregation.
- [x] Add limit evaluator tests for daily and weekly rules.
- [ ] Add reminder escalation state machine.
- [ ] Add force-close policy model and countdown states.

## Milestone 2 - Infrastructure
- [ ] Implement JSON storage.
- [ ] Implement Steam cache reader.
- [ ] Implement Steam process monitor.
- [ ] Implement Windows notification and sound services.
- [ ] Implement startup registry manager.
- [ ] Implement JSON/CSV export.

## Milestone 3 - WinUI App
- [ ] Create WinUI 3 app project after templates are available.
- [ ] Build Games page.
- [ ] Build Statistics page.
- [ ] Build Settings page.
- [ ] Build tray integration and configurable close behavior.

## Milestone 4 - Packaging and Validation
- [ ] Add portable publish script.
- [ ] Produce ZIP release artifact.
- [ ] Complete manual Windows acceptance test.
- [ ] Tag v0.1.0-winui.
