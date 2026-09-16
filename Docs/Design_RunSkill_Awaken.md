# 런 스킬 각성(액티브+패시브 조합) — 리서치 + 기획 초안 (260916)

> **상태: 리서치 + 아이디어 정리. 구현 없음.** 2-11-1에서 만든 런 스킬(뱀서라이크) 8종
> 위에 얹을 "액티브+패시브 조합 → 각성" 시스템을 위해, 실제 모바일 뱀서라이크
> 게임들이 이걸 어떻게 하고 있는지 찾아보고, 지금 있는 8종 + 땅따먹기 특유의 규칙에
> 맞춰 구체안을 짰다. 로컬에서 이어서 구현할 때 참고할 것.

---

## 1. 실제 게임들은 어떻게 하고 있나

세 게임(외계인은 배고파 / Vampire Survivors / Survivor.io)이 **완전히 같은 뼈대**를 쓴다 —
"액티브를 거의 만렙까지 올리고, 정해진 짝 패시브를 갖고 있으면, 그 액티브 자체가
다른 무언가로 바뀐다." 우연이 아니라 이 장르가 수렴한 정답에 가까워 보인다.

### 외계인은 배고파 — "초월"
액티브 스킬을 **5성**까지 올리고, 그 스킬 전용 **시너지 패시브를 1레벨 이상** 갖고 있으면
레벨을 한 단계 더 올려 **6성 "초월"**로 만들 수 있다. 초월하면 작동 방식이 살짝
바뀌면서 스펙이 크게 오른다. 조합은 액티브마다 정해진 짝이 있다(예: 통통 튀는 클로 +
신발류 패시브, 방어막류 + 회복 아이템류, 회전 칼날류 + 시계류 아이템).
([나무위키](https://namu.wiki/w/%EC%99%B8%EA%B3%84%EC%9D%B8%EC%9D%80%20%EB%B0%B0%EA%B3%A0%ED%8C%8C), [공략집](https://shinsm.com/%EC%99%B8%EA%B3%84%EC%9D%B8%EC%9D%80-%EB%B0%B0%EA%B3%A0%ED%8C%8C-%EA%B3%B5%EB%9E%B5/), [돌파 조합 정리](https://app.dcinside.com/board/ailenhungry/131))

### Vampire Survivors — "Evolution"
무기(액티브)를 **최대 레벨**(보통 8)까지 올리고, 정해진 **패시브 아이템을 아무 레벨이나**
들고 있는 채로 보스가 떨어뜨리는 상자를 열면, 무기가 고유 이름을 가진 **진화형**으로
바뀐다. 예: 도끼+촛대꽂이=죽음의나선, 채찍+텅빈심장=피눈물, 마늘+토마토=영혼포식자.
슬롯(무기 6개까지)을 늘리지 않고 그 자리에서 교체되는 게 핵심 — 진화해도 자리를
잡아먹지 않는다. ([Vampire Survivors Wiki – Evolution](https://vampire.survivors.wiki/w/Evolution), [PCGamesN 가이드](https://www.pcgamesn.com/vampire-survivors/weapon-evolutions))

### Survivor.io — "EVO"
무기 스킬을 **5성**까지 올리고 짝이 되는 **서포트(보급) 스킬**을 갖고 있으면 EVO 등급으로
전환된다. 발사 속도·피해량이 크게 오르고 겉모습도 바뀐다.
([BlueStacks 가이드](https://www.bluestacks.com/blog/game-guides/survivor-io/sio-skills-evolution-guide-en.html), [진화 목록](https://writerparty.com/party/survivor-io-full-evolution-list-and-evo-guide/))

### 대조군 — Brotato / Soul Knight Prequel (조합이 아니라 "결")
이 둘은 1:1 진화가 아니라 **같은 계열(근접/원거리, 무기 타입)에 몰아주면 보너스**를 주는
방식이다(Brotato의 "Blade" 시너지 = 검 계열을 모으면 회피+흡혈). 진화 카드는 없고
스탯이 겹겹이 쌓인다. ([Brotato 가이드](https://rogueliker.com/brotato-guide/), [Soul Knight Prequel 빌드](https://www.pocketgamer.com/soul-knight-prequel/builds/))
→ 참고는 되지만, 우리가 만든 CRunSkillEffect 구조(효과 모듈 하나가 정체성을 갖는 것)와는
결이 달라서 메인으로 삼기엔 안 맞는다. 오히려 지금 있는 "타입별 개별 효과" 방식이
외계인은 배고파/VS/Survivor.io 쪽에 훨씬 가깝다.

### 공통점 정리 (우리가 베낄 부분)
1. **액티브는 만렙 근처, 패시브는 아무 레벨이나** — 패시브는 "열쇠"지 "재료"가 아니다.
2. **조합은 정해진 표(1:1 또는 1:N)다.** 자유 조합이 아니다 — 조합 수를 통제하고,
   조합마다 손으로 만든 이름·연출을 붙이기 위해서다.
3. **각성해도 슬롯을 새로 먹지 않는다.** 액티브가 그 자리에서 그대로 강해진다.
4. **동작 자체가 바뀐다.** 그냥 수치만 올리는 게 아니라(예: +50% 피해),
   회전탄이 궤도를 벗어나거나 몽둥이가 지형을 바꾸는 식으로 **패턴이 달라진다** —
   이게 "각성했다"는 느낌을 만드는 진짜 이유다.

---

## 2. 이 프로젝트에 얹을 구조 (안)

기존 `CRunSkillEffect`/`RunSkillInfo.csv` 골격을 그대로 쓰고, 표 하나만 얹는다.

```
AwakenInfo.csv
──────────────
iAwakenID   eActiveType   ePassiveType   strName   strDesc
iRequireActiveLevel(보통 만렙)   iRequirePassiveLevel(보통 1)
fValueOverride / iCountOverride 등 각성 후 수치
```

- **조회 방향은 액티브 기준.** `CRunSkillHandler`가 이미 타입별 레벨을 Dictionary로
  들고 있으니, "지금 액티브가 만렙이고 표에 적힌 짝 패시브를 1레벨 이상 갖고 있는가"는
  기존 자료구조만으로 바로 계산된다 — 새 상태를 추가할 필요가 없다.
- **3지선다에 "각성 가능" 카드가 별도로 뜬다.** 조건이 맞으면 일반 스킬 후보 대신(또는
  같이) "각성: XXX" 카드 하나가 후보 풀에 섞인다 — Vampire Survivors의 "상자"처럼
  일반 픽과는 다르게 보이도록(테두리 색을 다르게, 예를 들어 CARD_TYPE/RUN_SKILL과
  또 다른 톤) 구분할 것.
- **각성은 CRunSkillEffect 안에서 처리한다.** `On_Awaken(CAwakenInfo cInfo)` 같은 훅을
  추가해서, 효과 모듈 내부에서 동작 자체를 바꾼다(예: `CRunSkillEffect_Orbit`이
  `m_bAwakened` 플래그를 하나 갖고 `Tick`/`Try_Get_OrbitOffsets`에서 분기). **새 스킬
  클래스를 만들지 않는다** — 각성은 "다른 스킬"이 아니라 "그 스킬의 강화된 형태"라서,
  같은 클래스 안에서 분기하는 게 2-6/2-11의 조합 철학(같은 것은 한 곳에)과 맞는다.
- **패시브 슬롯은 그대로 남는다.** VS의 "무기만 진화, 패시브는 계속 살아 있다"와 같다 —
  패시브 자체 효과(예: 자석 반경)는 각성 후에도 독립적으로 계속 작동한다.

---

## 3. 지금 있는 8종으로 짜 본 조합 (구체안)

액티브가 회전탄·몽둥이 둘뿐이라 조합 후보는 자연히 "회전탄×6패시브", "몽둥이×6패시브" 중
일부를 고르는 모양이 된다. 전부 다 만들 필요는 없고, 각각 성격이 겹치지 않게 몇 개만
먼저 골랐다.

| 조합 | 이름(가칭) | 효과 |
|---|---|---|
| 회전탄 + 자석 | **블랙홀** | 회전탄이 주변 몬스터·적탄을 끌어당겨 한 점에 모았다가 터뜨린다 — 자석의 "끌어당김"을 전투로 확장 |
| 회전탄 + 분노조절못해 | **광란의 칼바람** | 피버타임(분노 게이지 만땅) 동안 회전탄 개수·회전 속도가 크게 는다 — 두 시스템이 서로 몰랐던 채로 자연히 겹친다(2-10-2 흔들림/펀치와 같은 결) |
| 회전탄 + 어디로든 신발 | **궤도 이탈** | 회전탄 하나가 이따금 궤도를 벗어나 화면 끝까지 날아갔다가 반대편에서 돌아온다 — 신발의 랩어라운드 개념을 투사체에 적용 |
| 몽둥이 + 월보 | **땅고르기 몽둥이** | 몽둥이로 때린 자리가 트레일이 아니라 **그 즉시 점령지로 바뀐다** — 전투와 이 게임 고유의 영토 메커니즘을 직접 잇는, 다른 뱀서라이크에는 있을 수 없는 조합 |
| 몽둥이 + 회피 | **반격의 몽둥이** | 회피에 성공하면 몽둥이 쿨타임이 즉시 다 찬다(반격) |
| 몽둥이 + 영혼 수집가 | **영혼 사냥꾼** | 몽둥이에 맞아 죽은 몬스터가 확정으로 영혼을 떨어뜨린다 |

**월보/몽둥이 조합("땅고르기 몽둥이")이 가장 이 게임다운 각성이라고 본다** — 나머지는
장르 공식(광역화·투사체 변형·쿨타임 환원)을 그대로 옮긴 것에 가깝지만, 이건 "전투가
영토를 만든다"는 이 게임만의 핵심 재미를 스킬로 표현한다. 각성 시스템을 하나만
먼저 만들어 본다면 이걸 먼저 권한다.

---

## 4. 땅따먹기 테마로 새로 만들어 볼 만한 패시브 후보

뱀서라이크 장르 공식(자석/회피/분노 등)을 넘어서, **그리드·영토 규칙과 직접 엮이는**
스킬을 몇 개 적어 둔다 — 다른 게임엔 없는, 이 프로젝트만의 스킬이 될 수 있는 것들.

- **가시 울타리**(패시브): 점령지 **경계선**에 닿아 있는 몬스터가 지속 피해를 입는다.
  "경계선 위에서만 안전하다"(2-3)는 기존 규칙 위에 자연스럽게 얹힌다.
- **메아리**(패시브): 마지막으로 그은 선을 몇 초 뒤 같은 모양대로 자동으로 한 번 더
  그어 준다(몬스터가 없을 때만) — 선 굿기 자체를 두 배로 활용.
- **동상**(액티브 후보): 지나간 트레일 칸을 잠깐 얼린다 — 밟은 몬스터가 그 자리에 묶인다.
- **이정표**(패시브): 방향을 처음 꺾을 때마다 잠깐 가속 — "계속 꺾어야 하는" 이 장르의
  조작감에 보상을 준다.

이 넷은 지금 8종처럼 CRunSkillEffect 하나씩으로 바로 짤 수 있는 크기다 — 각성 시스템과
별개로, 스킬 풀을 늘리고 싶을 때 다음 후보로 써도 된다.

---

## 5. 다음에 정할 것

- `AwakenInfo.csv`의 정확한 열(수치 오버라이드를 얼마나 유연하게 둘지)은 각성 후보를
  몇 개나 먼저 만들지 정한 뒤 확정하는 게 낫다 — 지금은 회전탄/몽둥이 각 1~2개만
  있어도 표 모양이 나온다.
- 3지선다 UI에 "각성 카드"를 어떻게 구분해 보여줄지(테두리 색/아이콘)는 2-11-1에서
  이미 미룬 "3지선다에 스킬 섞기" 작업과 같이 갈 것 — 로컬에서 `CUI_CardPick` 일반화할
  때 한 번에 설계하는 게 UI를 두 번 만지지 않는 길이다.
- 새 패시브(가시 울타리 등)를 실제로 넣을지는 스킬 풀이 8개로 충분한지 먼저 플레이해
  보고 정할 것 — 이 문서는 후보만 적어 뒀다.

---

## 참고 자료

- [외계인은 배고파 — 나무위키](https://namu.wiki/w/%EC%99%B8%EA%B3%84%EC%9D%B8%EC%9D%80%20%EB%B0%B0%EA%B3%A0%ED%8C%8C)
- [외계인은 배고파 공략집 — 스킬 조합/속성 상성](https://shinsm.com/%EC%99%B8%EA%B3%84%EC%9D%B8%EC%9D%80-%EB%B0%B0%EA%B3%A0%ED%8C%8C-%EA%B3%B5%EB%9E%B5/)
- [★★돌파 스킬 조합 정리★★ — 외계인은 배고파 마이너 갤러리](https://app.dcinside.com/board/ailenhungry/131)
- [Vampire Survivors Wiki — Evolution](https://vampire.survivors.wiki/w/Evolution)
- [Vampire Survivors 무기 진화 가이드 — PCGamesN](https://www.pcgamesn.com/vampire-survivors/weapon-evolutions)
- [Survivor.io Skills & Evolution Guide — BlueStacks](https://www.bluestacks.com/blog/game-guides/survivor-io/sio-skills-evolution-guide-en.html)
- [Survivor.io 전체 진화 목록 — WriterParty](https://writerparty.com/party/survivor-io-full-evolution-list-and-evo-guide/)
- [Brotato 가이드 — Rogueliker](https://rogueliker.com/brotato-guide/)
- [Soul Knight Prequel 빌드 — Pocket Gamer](https://www.pocketgamer.com/soul-knight-prequel/builds/)
