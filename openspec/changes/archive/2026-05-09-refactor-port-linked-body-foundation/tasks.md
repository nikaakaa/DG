## 1. Proposal Validation
- [x] 1.1 闃呰 `openspec/specs/shared-gamecore-entity-rules/spec.md` 涓?port graph 鍜?composite deferral 鐜版湁瑕佹眰銆?- [x] 1.2 纭鏈彉鏇翠笉寮曞叆鎸佷箙 `CompositeBodyEntity`銆?- [x] 1.3 纭鏈彉鏇翠笉寮曞叆 module銆亀heel銆乿ehicle 璇箟銆?
## 2. Port Linked Body Model
- [x] 2.1 鏄庣‘ `PortConnectorComponent` 鍙〃杈捐繛鎺ヨ兘鍔涖€?- [x] 2.2 鏄庣‘ port graph 鍙骇鍑?connected body connectivity銆?- [x] 2.3 鏄庣‘ connected body root 鏄?stable representative锛屼笉鏄?parent銆?- [x] 2.4 鏄庣‘ connected body members 鏄?flat set銆?
## 3. Component Semantics
- [x] 3.1 鏄庣‘ linked body 涓嶄紶鎾?member components銆?- [x] 3.2 鏄庣‘ `PushableComponent` 鏄?push entry 鑳藉姏銆?- [x] 3.3 鏄庣‘ movement permission 鏄?body-level 鑱氬悎妫€鏌ャ€?- [x] 3.4 鏄庣‘ blocking 浠嶇劧鏄?member 鍗犳牸浜嬪疄銆?
## 4. Capability Resolver
- [x] 4.1 璁捐 `BodyCapabilityResolver` 鎴栫瓑浠疯竟鐣屻€?- [x] 4.2 鏀寔 push entry 鍒ゆ柇銆?- [x] 4.3 鏀寔 body movability 鍒ゆ柇銆?- [x] 4.4 鏀寔 body blocking/occupancy 鍒ゆ柇銆?- [x] 4.5 淇濇寔 control 鑱氬悎涓洪鐣欒竟鐣屻€?
## 5. Action Integration
- [x] 5.1 action unit 淇濈暀 entry entity 璇箟銆?- [x] 5.2 connected body action 閫氳繃 body subject 瑙勫垝 claims銆?- [x] 5.3 linked body commit 淇濇寔 all-or-nothing銆?- [x] 5.4 鏅€?part 涓嶈兘缁曡繃 connected body action 鍗曠嫭绉诲姩銆?
## 6. Cache Boundary
- [x] 6.1 棰勭暀 `PortGraphCache` / `ConnectedBodyCache` 鍛藉悕鍜岃亴璐ｃ€?- [x] 6.2 鏄庣‘ cache 鍙敱 final component/world state 閲嶅缓銆?- [x] 6.3 鏄庣‘ dirty 鏉ユ簮鍖呮嫭 port銆乸osition銆乨irection銆乻pawn銆乺emove銆?- [x] 6.4 鏈樁娈典笉瑕佹眰瀹炵幇鍏ㄩ噺缂撳瓨浼樺寲銆?
## 7. Automated Tests
- [x] 7.1 娣诲姞 linked pushable entry 鎺ㄥ姩 whole body 鐨?Unity EditMode 娴嬭瘯銆?- [x] 7.2 娣诲姞 linked non-pushable entry 鎷掔粷 push 鐨?Unity EditMode 娴嬭瘯銆?- [x] 7.3 娣诲姞 linked body 涓嶄紶鎾?`PushableComponent` 鐨?Unity EditMode 娴嬭瘯銆?- [x] 7.4 娣诲姞 movement permission 闃绘 whole body 鐨?Unity EditMode 娴嬭瘯銆?- [x] 7.5 娣诲姞 external blocker 瑙﹀彂 all-or-nothing reject 鐨?Unity EditMode 娴嬭瘯銆?- [x] 7.6 娣诲姞 port cycle stable root/members 鐨?Unity EditMode 娴嬭瘯銆?
## 8. Verification
- [x] 8.1 杩愯 `openspec validate refactor-port-linked-body-foundation --strict --no-interactive`銆?- [x] 8.2 瀹炵幇闃舵杩愯 `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`銆?- [x] 8.3 瀹炵幇闃舵杩愯 `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`銆?- [x] 8.4 瀹炵幇闃舵杩愯 Unity TestFramework EditMode銆?- [x] 8.5 鐢ㄦ埛鎵嬪姩杩愯鏈嶅姟绔潈濞?Play Mode 鍜屽弻瀹㈡埛绔鍒扮楠岃瘉銆?
