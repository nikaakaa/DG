## 1. Proposal Validation
- [x] 1.1 杩愯 `openspec validate refactor-action-subject-policy --strict --no-interactive`銆?- [x] 1.2 杩愯 `openspec show refactor-action-subject-policy --json --deltas-only` 纭 delta 琚В鏋愩€?
## 2. Subject Policy Schema
- [x] 2.1 瀹¤ `ActionSubjectKind` 褰撳墠浣跨敤鐐广€?- [x] 2.2 灏?`SingleEntity` 璇箟鏄庣‘涓?`HitEntity`銆?- [x] 2.3 灏?`ConnectedBody` 璇箟鏄庣‘涓?`ConnectedBodyIfAny`銆?- [x] 2.4 纭娌℃湁鏂板骞宠 subject 鏋氫妇銆?
## 3. ActionSpec Policy Configuration
- [x] 3.1 瀹¤鐜版湁 `ActionSpecId`锛屽垪鍑哄摢浜涢厤缃渶瑕?`ConnectedBodyIfAny`銆?- [x] 3.2 灏嗛渶瑕?connected body 缁撶畻鐨勭幇鏈夐厤缃敼涓鸿鍙?subject policy銆?- [x] 3.3 淇濈暀 `connected_body_move` 鍏煎璺緞銆?- [x] 3.4 纭瑙勫垯灞傛病鏈夋牴鎹?`mechanism_push`銆乣connected_body_move` 鎴栧叾浠?action 鍚嶅瓧鍒嗘敮鍐冲畾 subject銆?- [x] 3.5 纭涓嶆柊澧炲叾浠?`connected_body_xxx` action銆?
## 4. Resolver Boundary
- [x] 4.1 纭 subject 瑙ｆ瀽鍙鍙?entry entity銆乣ActionSpec.subjectPolicy`銆乫inal component/tag 鍜?port connected body view銆?- [x] 4.2 纭 subject 瑙ｆ瀽涓嶄慨鏀?`GameWorld`銆?- [x] 4.3 纭 subject 瑙ｆ瀽涓嶇敓鎴?`MovePlan`銆?- [x] 4.4 纭 subject 瑙ｆ瀽涓嶅垽鏂?blocker銆?- [x] 4.5 纭 subject 瑙ｆ瀽涓嶅垱寤?handoff銆?
## 5. Automated Tests
- [x] 5.1 鐢?Unity TestFramework EditMode 瑕嗙洊 BodyResolver 浠庝换鎰?member 瑙ｆ瀽鍚屼竴 connected body銆?- [x] 5.2 瑕嗙洊涓€涓厤缃负 `ConnectedBodyIfAny` 鐨勭幇鏈?`ActionSpecId` 鍛戒腑 connected body member 鏃舵暣浣撶Щ鍔ㄣ€?- [x] 5.3 瑕嗙洊璇ラ厤缃懡涓?connected body member 涓旂洰鏍囪 blocker 闃绘尅鏃舵暣浣撲笉鍔ㄦ垨杩涘叆 policy 瑙勫畾鍒嗘敮銆?- [x] 5.4 瑕嗙洊 connected body 琚彟涓€涓?connected body 闃绘尅鏃惰繘鍏?handoff 涓斾笉鎷嗗垎 member銆?- [x] 5.5 瑕嗙洊鏅€?non-port entity 涓嶅彈 connected body subject policy 褰卞搷銆?- [x] 5.6 瑕嗙洊鍚屼竴涓?subject policy 鎹㈠埌鍙︿竴涓櫘閫?move-like `ActionSpecId` 鏃朵粛鐢?policy 鐢熸晥锛岃€屼笉鏄敱 action 鍚嶅瓧鐢熸晥銆?
## 6. Verification
- [x] 6.1 杩愯 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`銆?- [x] 6.2 杩愯 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`銆?- [x] 6.3 杩愯 Unity TestFramework EditMode 鐩稿叧娴嬭瘯銆?- [x] 6.4 浜ょ粰鐢ㄦ埛鎵嬪姩楠岃瘉 Play Mode / 鍙屽鎴风绔埌绔悓姝ャ€?
