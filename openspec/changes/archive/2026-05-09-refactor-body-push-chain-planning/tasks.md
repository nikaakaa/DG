## 1. Proposal Gate
- [x] 1.1 纭 `refactor-port-linked-body-foundation` 鐨?connected body view銆乸ush entry銆乥ody movability銆乤ll-or-nothing commit 宸插疄鐜版垨鎸変緷璧栭『搴忓畬鎴愩€?- [x] 1.2 璇诲彇鏈?change 鐨?`proposal.md`銆?- [x] 1.3 璇诲彇鏈?change 鐨?`design.md`銆?- [x] 1.4 璇诲彇鏈?change 鐨?spec deltas銆?- [x] 1.5 纭鏈?change 涓嶅紩鍏ユ寔涔?`CompositeBodyEntity`銆?- [x] 1.6 纭鏈?change 涓嶅仛鍚?tick 鍏ㄩ摼姹傝В銆?
## 2. Action Subject And Pending State
- [x] 2.1 姊崇悊鐜版湁 `PendingActionState` 涓?source銆乼arget銆乻ubject entity ids 鐨勮亴璐ｃ€?- [x] 2.2 澧炲姞鎴栨暣鐞?body-aware chain metadata銆?- [x] 2.3 璁板綍 parent action unit 涓?active child action unit 鐨勫叧绯汇€?- [x] 2.4 璁板綍褰撳墠閾剧殑 depth銆?- [x] 2.5 璁板綍褰撳墠閾?visited subject銆?- [x] 2.6 璁板綍 entry entity id銆?- [x] 2.7 淇濇寔涓€涓?parent 鍚屾椂鏈€澶氫竴涓?active child銆?- [x] 2.8 淇濇寔 owner action result 鍙湁 parent retry 鎴愬姛鍚庢墠 success銆?
## 3. Body Handoff Resolution
- [x] 3.1 鍦?blocked policy 涓В鏋?blocker 鐨?subject kind銆?- [x] 3.2 blocker 鏄?connected body 鏃惰В鏋?stable body subject銆?- [x] 3.3 blocker 鏄?single entity 鏃朵繚鐣?single entity subject銆?- [x] 3.4 浣跨敤 `BodyCapabilityResolver` 鍒ゆ柇 touched member 鏄惁涓哄悎娉?push entry銆?- [x] 3.5 鍚堟硶 body entry 鐢熸垚 body subject child action銆?- [x] 3.6 闈炴硶 body entry 鐢熸垚绋冲畾 rejected result銆?- [x] 3.7 body 涓?source 灞炰簬鍚屼竴 subject 鏃朵笉鍒涘缓澶栭儴 handoff銆?
## 4. Nested Push Propagation
- [x] 4.1 鍏佽 derived action unit 琚?pushable blocker 闃绘尅鏃剁户缁淳鐢?child銆?- [x] 4.2 child 鎴愬姛鍚庡彧鍞ら啋鐩存帴 parent retry銆?- [x] 4.3 parent retry 鏃堕噸鏂拌鍙栧綋鍓?world銆?- [x] 4.4 child failure 鏃剁洿鎺?parent failed銆?- [x] 4.5 parent failure 鍚?owner action 浼犳挱绋冲畾 reason銆?- [x] 4.6 淇濇寔姣忎釜 unit commit 鍙鐩栬嚜宸辩殑 subject銆?
## 5. Chain Safety
- [x] 5.1 瀹氫箟鏈€澶?push chain depth 榛樿鍊笺€?- [x] 5.2 瓒呰繃鏈€澶?depth 鏃舵嫆缁濆綋鍓?unit銆?- [x] 5.3 visited subject 閲嶅鏃舵嫆缁濆綋鍓?unit銆?- [x] 5.4 parent 宸叉湁 active child 鏃舵嫆缁濋噸澶?child銆?- [x] 5.5 pending 瓒呮椂鏃舵嫆缁?owner action銆?- [x] 5.6 澶辫触 reason 鍖哄垎 depth銆乧ycle銆乶on-pushable entry銆乥locked銆乼imeout銆?
## 6. Planning And Commit Boundaries
- [x] 6.1 纭 accepted body action 鍙鍒?body members銆?- [x] 6.2 纭 body action 涓嶆妸涓嬫父 blocker 鍔犺繘鏈?unit member commit銆?- [x] 6.3 纭 same-body old cells 涓嶇畻 external blocker銆?- [x] 6.4 纭 external blocker 浠嶇敱 blocked policy 娲剧敓鎴栨嫆缁濄€?- [x] 6.5 纭 dirty delta 鍙寘鍚涓?member position update銆?- [x] 6.6 纭 action result 浠嶅睘浜庡綋鍓?unit subject銆?
## 7. Unity TestFramework EditMode Tests
- [x] 7.1 娣诲姞 single entity push single entity 鐨勫洖褰掓祴璇曘€?- [x] 7.2 娣诲姞 single entity push connected body 鐨勫洖褰掓祴璇曘€?- [x] 7.3 娣诲姞 connected body push connected body 涓嶆媶 member 鐨勬祴璇曘€?- [x] 7.4 娣诲姞涓夋 connected body chain 閫氳繃 retry 渚濇绉诲姩鐨勬祴璇曘€?- [x] 7.5 娣诲姞 connected body 鍛戒腑 non-pushable member 鎷掔粷浼犳挱鐨勬祴璇曘€?- [x] 7.6 娣诲姞 connected body 涓嬫父 movement permission 澶辫触鎷掔粷鏁寸粍鐨勬祴璇曘€?- [x] 7.7 娣诲姞 push cycle guard 鐨勬祴璇曘€?- [x] 7.8 娣诲姞 max depth guard 鐨勬祴璇曘€?- [x] 7.9 娣诲姞 parent 鍙湁鍦ㄨ嚜韬?retry commit 鍚庢墠鎴愬姛鐨勬祴璇曘€?
## 8. Server Verification Tests
- [x] 8.1 鍦?authoritative move verification 涓鐩?body pushes body銆?- [x] 8.2 鍦?authoritative move verification 涓鐩?multi-body retry chain銆?- [x] 8.3 鍦?authoritative move verification 涓鐩?body chain failure 涓嶄骇鐢?partial commit銆?- [x] 8.4 鍦?authoritative move verification 涓鐩?WorldDelta 澶?member 鍧愭爣鍙樺寲銆?
## 9. Automated Validation
- [x] 9.1 杩愯 `openspec validate refactor-body-push-chain-planning --strict --no-interactive`銆?- [x] 9.2 杩愯 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`銆?- [x] 9.3 杩愯 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`銆?- [x] 9.4 杩愯 Unity TestFramework EditMode 鐩稿叧娴嬭瘯銆?- [x] 9.5 璁板綍鏈繍琛岄」鍜屽師鍥犮€?
## 10. Manual End-to-End Verification
- [x] 10.1 鐢ㄦ埛鎵嬪姩鍚姩鏈嶅姟绔潈濞?Play Mode銆?- [x] 10.2 鐢ㄦ埛鎵嬪姩鍚姩鍙屽鎴风銆?- [x] 10.3 鐢ㄦ埛鎽嗘斁妯帓澶氫釜 connected body銆?- [x] 10.4 鐢ㄦ埛浠庡乏渚ф帹鍔ㄦí鎺掔粍鍚堜綋銆?- [x] 10.5 楠岃瘉鏈€鍙充晶 body 涓嶈鎷嗘垚鏅€氬崟 entity push銆?- [x] 10.6 楠岃瘉鏈嶅姟绔拰涓や釜瀹㈡埛绔渶缁?WorldDelta 涓€鑷淬€?
