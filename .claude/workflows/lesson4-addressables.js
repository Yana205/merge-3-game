export const meta = {
  name: 'lesson4-addressables',
  description: 'Implement course Lesson 4 (Addressables migration, async ServiceLoader, layered architecture) with ordered subagents, then verify against the assignment checklist',
  whenToUse: 'PREREQUISITES: Lesson 3 PR merged to main; branch feature/lesson4-addressables checked out; Unity editor OPEN with the MCP bridge connected (package install + Addressables setup need it). Executes docs/plans/LESSON4_ADDRESSABLES_PLAN.md.',
  phases: [
    { title: 'Preflight', detail: 'branch, lesson-3 code, Unity MCP reachable' },
    { title: 'Addressables', detail: 'package + GemItem key in Gameplay group' },
    { title: 'ServiceLoader', detail: 'async LoadAsync + injection' },
    { title: 'Architecture', detail: 'ItemFactory + 4-layer diagram' },
    { title: 'Missions', detail: 'missions_done.md / missions_to_do.md' },
    { title: 'Verify', detail: 'assignment checkers + compile check' },
    { title: 'Fix', detail: 'apply review findings' },
  ],
}

const REPO = '/Users/yanai/Desktop/unitygame/unitytestsprojects/sbs 1'
const BRANCH = 'feature/lesson4-addressables'

const REPORT = {
  type: 'object',
  required: ['ok', 'summary', 'files', 'commit', 'notes'],
  properties: {
    ok: { type: 'boolean' },
    summary: { type: 'string' },
    files: { type: 'array', items: { type: 'string' } },
    commit: { type: 'string', description: 'short hash, or "" if no commit' },
    notes: { type: 'string', description: 'caveats / manual follow-ups, "" if none' },
  },
}

const VERDICT = {
  type: 'object',
  required: ['pass', 'problems'],
  properties: {
    pass: { type: 'boolean' },
    problems: { type: 'array', items: { type: 'string' } },
  },
}

const CTX = [
  'You are one step in an ordered pipeline implementing a Unity course assignment (Lesson 4: data formats, async, Addressables, game architecture) in the merge-3 game repo at "' + REPO + '" (path contains a space - always quote it).',
  'Ground rules from CLAUDE.md: code in Assets/_Project/Scripts/; one class per file, file name = class name; PascalCase public, _camelCase private; [SerializeField] private over public; [Header] sections; Debug.LogError null-checks. NEVER hand-edit .unity/.prefab/.asset/.mat/.meta files - use the Unity MCP tools (ToolSearch "select:mcp__ai-game-developer__<tool>") or the MCP script-execute tool for editor-API work (AddressableAssetSettings etc.).',
  'FIRST ACTION: git -C "' + REPO + '" branch --show-current must print ' + BRANCH + '; otherwise stop, change nothing, return ok=false.',
  'After creating .cs files call the MCP assets-refresh tool and commit generated .meta files alongside.',
  'Commit at the end of your phase with the given message, ending with a blank line then: Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>',
  'Do NOT push. Do NOT open PRs.',
].join('\n')

phase('Preflight')
const pre = await agent('You are a READ-ONLY preflight checker for repo "' + REPO + '". Verify ALL of: (1) git -C "' + REPO + '" branch --show-current prints ' + BRANCH + '; (2) Lesson 3 code is present: Assets/_Project/Scripts/Core/Pooling/IPool.cs, MonoBehaviourPool.cs and Core/ItemPoolManager.cs exist; (3) the Unity editor MCP bridge is reachable: load via ToolSearch "select:mcp__ai-game-developer__assets-refresh" and call it - reachable means it returns without a connection error. Change nothing. pass=true only if all three hold; report each failure as a problem string.', { label: 'preflight', phase: 'Preflight', schema: VERDICT })
if (!pre || !pre.pass) {
  return { aborted: 'Preflight failed - fix these before running lesson 4', problems: pre ? pre.problems : ['preflight agent returned nothing'] }
}
log('Preflight OK')

phase('Addressables')
const addr = await agent(CTX + '\n\nYOUR PHASE - Part 1, Addressables migration of the gem Item prefab (Assets/_Project/Prefabs/Item.prefab), currently a hard-wired Inspector reference used with Instantiate.\n\n1. Install com.unity.addressables: use the MCP package-add tool (ToolSearch "select:mcp__ai-game-developer__package-add"); it may trigger a domain reload - wait for the result. Commit the Packages/manifest.json (and packages-lock.json) change.\n2. Mark Item.prefab Addressable via the MCP script-execute tool running editor code (UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject / AddressableAssetSettings API): create a named group "Gameplay" (NOT Default Local Group), create/move the entry for Item.prefab with address key "GemItem". Save settings (AssetDatabase.SaveAssets).\n3. Commit the generated Assets/AddressableAssetsData/ files (these are Unity-serialized - commit them, never hand-edit).\n4. Build Addressables via script-execute: AddressableAssetSettings.BuildPlayerContent(). Report any build errors.\n5. Add a "// ADDRESSABLES MIGRATION:" comment block at the top of the script that will own loading (Assets/_Project/Scripts/Core/Loader.cs for now; the next phase renames it): migrated = Item.prefab; old method = Inspector-wired reference + Instantiate; new key = GemItem; group = Gameplay.\n\nCommit message (first line):\nfeat: item prefab as Addressable (GemItem, Gameplay group)', { label: 'addressables', phase: 'Addressables', schema: REPORT })
if (!addr || !addr.ok) return { aborted: 'Addressables phase failed', pre, addr }
log('Addressables done: ' + addr.summary)

phase('ServiceLoader')
const svc = await agent(CTX + '\n\nYOUR PHASE - Part 2, async ServiceLoader. Prior phase report: ' + JSON.stringify(addr) + '\n\n1. Rename Assets/_Project/Scripts/Core/Loader.cs to ServiceLoader.cs (file AND class; use git mv then edit; run MCP assets-refresh; the scene reference to the old component will need re-checking - see step 5). Keep the existing ISaveSystem -> ScoreController wiring.\n2. Add: public async Task LoadAsync() which (a) awaits Addressables.LoadAssetAsync<GameObject>("GemItem"), (b) checks handle.Status == AsyncOperationStatus.Succeeded - on failure Debug.LogError(handle.OperationException != null ? handle.OperationException.Message : "load failed") and return, (c) creates a MonoBehaviourPool<Item> and Init(prefab, 32), (d) injects into the factory: _itemFactory.Init(prefab, pool) (the factory class arrives in the next phase - create a minimal ItemFactory stub now if needed so this compiles, the next phase completes it).\n3. NO async void anywhere. Entry point: void Start() => _ = LoadAsync();\n4. Readiness gate: expose public event Action OnServicesReady, fired at the end of a successful LoadAsync. The component that starts the first level (Bootstrap/GameManager/LevelManager - read them and pick the actual entry point) must wait for it before creating the board, with a clear Debug.LogError path if services never load.\n5. Scene wiring via MCP (gameobject-find / gameobject-component-add / gameobject-component-modify / scene-save): make sure the renamed ServiceLoader component is on the right GameObject with scoreController assigned, and remove any now-broken Loader reference. Never hand-edit the scene file.\n6. "// SERVICE LOADER DESIGN:" comment block: assets loaded (GemItem prefab), objects created (MonoBehaviourPool<Item>, PlayerPrefsSaveSystem), injections (prefab+pool -> ItemFactory, ISaveSystem -> ScoreController).\n\nCommit message (first line):\nfeat: async ServiceLoader loads GemItem and injects pool', { label: 'service loader', phase: 'ServiceLoader', schema: REPORT })
if (!svc || !svc.ok) return { aborted: 'ServiceLoader phase failed', pre, addr, svc }
log('ServiceLoader done: ' + svc.summary)

phase('Architecture')
const arch = await agent(CTX + '\n\nYOUR PHASE - Part 3, layered architecture. Prior reports: ' + JSON.stringify({ addr, svc }) + '\n\nTarget layering (top to bottom): 1 CONTROLLER (GameManager/Bootstrap/ServiceLoader - lifecycle, holds ONLY manager references) -> 2 MANAGERS (LevelManager, GridManager, MergeManager, InputHandler, UIManager - game rules) -> 3 FACTORY (ItemFactory - creates configured Items, no game logic) -> 4 SERVICES/DATA (MonoBehaviourPool<Item>, Addressables, ISaveSystem, GemConfig/LevelData).\n\n1. Complete Assets/_Project/Scripts/Core/ItemFactory.cs: plain C# class; EXACTLY ONE public Init(GameObject prefab, IPool<Item> pool) method; public Item Get(Vector3 position) and public void Release(Item item) delegating to the pool (Release calls item.ResetForPool() first if that lives here rather than in the pool manager - keep reset in exactly one place); NO game logic (no tier math, no scoring, no grid knowledge).\n2. Rewire so GridManager (and anything else spawning Items) calls the factory - never Instantiate, never the pool directly. Decide and document what remains of Lesson 3\'s ItemPoolManager: either it becomes the thin MonoBehaviour host that owns the factory, or it is removed in favor of ServiceLoader injection - pick the simpler wiring that satisfies: GameManager (top controller) must NOT reference the factory, the pool, or the prefab - only managers.\n3. Enforce and verify with greps: GameManager holds only manager references; GridManager calls factory.Get(); ItemFactory has no UnityEngine.Object.Instantiate calls outside... (the pool does instantiation; the factory must not).\n4. Write docs/ARCHITECTURE.md: the 4-layer ASCII diagram with labelled arrows (starts-levels/injects, factory.Get-never-Instantiate, pool.Get/Release) and a one-sentence responsibility per layer. Also add the same diagram as a comment block at the top of GameManager.cs.\n5. Scene wiring via MCP if component fields changed; scene-save. Run MCP assets-refresh + console-get-logs and fix any compile errors before committing.\n\nCommit message (first line):\nrefactor: layered architecture with ItemFactory', { label: 'architecture', phase: 'Architecture', schema: REPORT })
if (!arch || !arch.ok) return { aborted: 'Architecture phase failed', pre, addr, svc, arch }
log('Architecture done: ' + arch.summary)

phase('Missions')
const missions = await agent(CTX + '\n\nYOUR PHASE - bonus missions files. Create at the REPO ROOT:\n1. missions_done.md - at least 5 entries; seed from real git history (git log --oneline main): grid+merge core, level system, gem art for 20 tiers, themed background + styled UI buttons + main menu, Lesson 3 cache audit + IPool<T> + gem pooling, Lesson 4 Addressables + ServiceLoader + ItemFactory layering.\n2. missions_to_do.md - at least 5 entries; seed from docs/plans/COURSE_ROADMAP.md and the plan bonuses: SeasonalSkins Addressables group + runtime skin swap, merge VFX/juice (pooled), audio manager, save-slot expansion, lesson 5+ placeholder.\nFormat both as markdown checklists with one line of context per entry (AI tools use these to resume work next session).\n\nCommit message (first line):\ndocs: missions_done and missions_to_do trackers', { label: 'missions', phase: 'Missions', schema: REPORT })
log(missions && missions.ok ? 'Missions done' : 'Missions phase had problems')

phase('Verify')
const CHECKCTX = 'You are a READ-ONLY reviewer of repo "' + REPO + '" on branch ' + BRANCH + '. Inspect with git diff main...HEAD and by reading files; modify NOTHING. pass=true only if EVERY item holds; list each failure as a problem with file:line refs.\n\n'
const checks = await parallel([
  () => agent(CHECKCTX + 'Part 1 - Addressables: com.unity.addressables in Packages/manifest.json; AddressableAssetsData committed with a group named Gameplay (not Default Local Group) containing the Item.prefab entry keyed GemItem; no Inspector-wired itemPrefab left as the runtime source (fallback allowed only if clearly error-logged); loading code uses Addressables.LoadAssetAsync<GameObject>("GemItem") with async/await and checks AsyncOperationStatus.Succeeded before handle.Result; "// ADDRESSABLES MIGRATION:" block lists what/old/new-key/group.', { label: 'verify:addressables', phase: 'Verify', schema: VERDICT }),
  () => agent(CHECKCTX + 'Part 2 - ServiceLoader: class ServiceLoader with public async Task LoadAsync(); zero async void in the project (grep "async void"); Start uses _ = LoadAsync(); handle.Status verified before Result; OperationException message logged on failure; pool created and factory Init-injected inside LoadAsync; "// SERVICE LOADER DESIGN:" block lists loads/creates/injections; game start is gated on services ready.', { label: 'verify:serviceloader', phase: 'Verify', schema: VERDICT }),
  () => agent(CHECKCTX + 'Part 3 - Architecture: docs/ARCHITECTURE.md exists with >=4 named layers, labelled arrows, one-sentence responsibility each; ItemFactory has a single Init(prefab, pool), no game logic, no Instantiate; GridManager spawns only via factory.Get() (grep Instantiate over Assets/_Project/Scripts - every hit must be pool internals, cell creation, or explicit logged fallback); GameManager references managers only - grep it for ItemFactory, IPool, MonoBehaviourPool, itemPrefab: all must be absent.', { label: 'verify:architecture', phase: 'Verify', schema: VERDICT }),
  () => agent(CHECKCTX + 'Compile & bonus: via MCP (assets-refresh + console-get-logs) confirm zero compile errors - include error text if any; every new .cs/folder has a committed .meta; missions_done.md and missions_to_do.md exist at repo root with >=5 entries each.', { label: 'verify:compile', phase: 'Verify', schema: VERDICT }),
])
const problems = []
checks.forEach((c, i) => {
  if (!c) problems.push('Checker #' + (i + 1) + ' did not return a verdict - re-review manually.')
  else if (!c.pass) problems.push(...c.problems)
})
log(problems.length === 0 ? 'All checks passed' : problems.length + ' problem(s) found')

phase('Fix')
let fix = null
if (problems.length > 0) {
  fix = await agent(CTX + '\n\nYOUR PHASE - Fix round. Reviewers found these problems on this branch:\n- ' + problems.join('\n- ') + '\n\nPhase reports: ' + JSON.stringify({ addr, svc, arch, missions }) + '\n\nFix every code-level problem; use MCP for any editor-side fix. Problems you cannot fix: list in notes as user follow-ups.\n\nCommit message (first line):\nfix: address lesson 4 review findings', { label: 'fix findings', phase: 'Fix', schema: REPORT })
  log(fix ? 'Fix round done: ' + fix.summary : 'Fix agent returned nothing')
}

return {
  branch: BRANCH,
  phases: { addressables: addr, serviceLoader: svc, architecture: arch, missions: missions },
  problemsFound: problems,
  fixRound: fix,
  userFollowUps: [addr, svc, arch, missions, fix].filter(Boolean).map(r => r.notes).filter(n => n && n.length > 0),
}
