export const meta = {
  name: 'lesson3-pooling',
  description: 'Implement course Lesson 3 (cache audit, IPool<T>, gem pooling) with ordered subagents, then verify against the assignment checklist',
  whenToUse: 'Run with branch feature/lesson3-pooling checked out (off main). Executes docs/plans/LESSON3_POOLING_PLAN.md phase by phase, committing per phase. Does not push.',
  phases: [
    { title: 'Cache Audit', detail: 'fix 4 caching problems + CACHE AUDIT blocks' },
    { title: 'IPool', detail: 'IPool<T> + MonoBehaviourPool<T>' },
    { title: 'Pool Gems', detail: 'ItemPoolManager + rewire spawn/destroy' },
    { title: 'Verify', detail: 'assignment part checkers + compile check' },
    { title: 'Fix', detail: 'apply review findings' },
  ],
}

const REPO = '/Users/yanai/Desktop/unitygame/unitytestsprojects/sbs 1'
const BRANCH = 'feature/lesson3-pooling'

const REPORT = {
  type: 'object',
  required: ['ok', 'summary', 'files', 'commit', 'notes'],
  properties: {
    ok: { type: 'boolean', description: 'false if you had to stop (wrong branch, blocked)' },
    summary: { type: 'string', description: '2-4 sentences: what you changed' },
    files: { type: 'array', items: { type: 'string' }, description: 'repo-relative paths touched' },
    commit: { type: 'string', description: 'short hash of the commit you made, or "" if none' },
    notes: { type: 'string', description: 'caveats: missing .meta files, MCP unreachable, manual wiring steps for the user, etc. "" if none' },
  },
}

const VERDICT = {
  type: 'object',
  required: ['pass', 'problems'],
  properties: {
    pass: { type: 'boolean' },
    problems: { type: 'array', items: { type: 'string' }, description: 'each unsatisfied checklist item, with file:line references. Empty if pass.' },
  },
}

const CTX = [
  'You are one step in an ordered pipeline implementing a Unity course assignment (Lesson 3: caching, object pooling, generics) in the merge-3 game repo at "' + REPO + '" (the path contains a space - always quote it in shell commands).',
  'Ground rules from CLAUDE.md: all code lives in Assets/_Project/Scripts/; one class per file and file name = class name; PascalCase public members, _camelCase private fields; prefer [SerializeField] private over public fields for inspector references; use [Header("...")] sections; Debug.LogError null-checks before using serialized references.',
  'NEVER edit .unity, .prefab, .asset, .mat or .meta files by hand.',
  'FIRST ACTION: run git -C "' + REPO + '" branch --show-current and confirm it prints ' + BRANCH + '. If it does not, STOP immediately: make no changes, return ok=false with the mismatch in notes. Do not switch branches yourself.',
  'After creating new .cs files or folders: load the Unity MCP refresh tool via ToolSearch query "select:mcp__ai-game-developer__assets-refresh" and call it so the editor generates .meta files, then git add the new .meta files along with your code. If the MCP tool is unavailable or errors, proceed without metas and say so in notes.',
  'When your phase is complete, git add your files and commit with the exact message given below, ending the commit message with a blank line and then: Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>',
  'Do NOT push. Do NOT open PRs. Do NOT enter Unity play mode.',
].join('\n')

phase('Cache Audit')
log('Phase 3.1: cache audit (4 fixes)')
const audit = await agent(CTX + '\n\nYOUR PHASE - 3.1 Cache Audit. Fix these four verified caching problems:\n\n' + [
  '1. Assets/_Project/Scripts/Input/InputHandler.cs: GetMouseWorldPos() calls Camera.main on every call, and it runs every Update frame while dragging. Add a private Camera _camera field assigned in Awake() (Debug.LogError if Camera.main is null there), use the cached field in GetMouseWorldPos(), keep a null guard.',
  '2. Assets/_Project/Scripts/Core/GridManager.cs: ClearGrid() calls FindObjectsByType<Item>(...) - a whole-scene scan. Replace with owned bookkeeping: (a) add a private readonly List<Item> _liveItems; (b) SpawnItem() registers the spawned item; (c) add a new public void DespawnItem(Item item) that unregisters and destroys via the existing SafeDestroy; (d) in ClearGrid, when Application.isPlaying, despawn a copy of _liveItems and clear the list - keep the FindObjectsByType scan ONLY as the edit-mode fallback (editor tooling calls ClearGrid outside play mode, where the non-serialized list can be stale); (e) Assets/_Project/Scripts/Core/MergeManager.cs currently calls Destroy(itemA.gameObject), Destroy(itemB.gameObject) and Instantiate(gridManager.itemPrefab, ...) directly in TryMerge - rewire it to gridManager.DespawnItem(itemA), gridManager.DespawnItem(itemB), then Item newItem = gridManager.SpawnItem(cellB, newTier); (cellB is free at that point because RemoveItem already ran). Null-check newItem before scoring. This centralizes all Item lifetime in GridManager - later phases build on it.',
  '3. Assets/_Project/Scripts/Core/Cell.cs: CreateSquareSprite() builds a new 64x64 Texture2D for every cell that lacks a sprite - a 5x5 grid can create 25 identical textures. Cache it in a static Sprite field, exactly like GetWhiteSquare() in Assets/_Project/Scripts/Core/Item.cs.',
  '4. Assets/_Project/Scripts/Input/InputHandler.cs: HandlePointerDown/HandlePointerUp call col.GetComponent<Item>() / col.GetComponent<Cell>() per collider hit - switch these to col.TryGetComponent(out ...).',
].join('\n') + '\n\nThen add a "// CACHE AUDIT" comment block at the top of each fixed script (InputHandler.cs, GridManager.cs, MergeManager.cs, Cell.cs) listing what was fixed and where in that file. In the GridManager.cs block also note: the project was searched for "new WaitForSeconds" in loops and none exist. No behavior change intended - reread your diff to confirm.\n\nCommit message (first line):\nrefactor: cache audit - camera, scene scans, shared sprites', { label: '3.1 cache audit', phase: 'Cache Audit', schema: REPORT })
if (!audit || !audit.ok) return { aborted: 'Phase 3.1 failed or stopped', audit }
log('3.1 done: ' + audit.summary)

phase('IPool')
log('Phase 3.2: IPool<T> + MonoBehaviourPool<T>')
const pool = await agent(CTX + '\n\nYOUR PHASE - 3.2 Generic pool. Create the folder Assets/_Project/Scripts/Core/Pooling/ with exactly two files:\n\n1. IPool.cs - the assignment requires this exact interface shape (no namespace, matching the rest of the project):\npublic interface IPool<T> { T Get(); void Release(T item); }\n\n2. MonoBehaviourPool.cs - public class MonoBehaviourPool<T> : IPool<T> where T : Component. A plain C# class, NOT a MonoBehaviour. Implement:\n- public void Init(GameObject prefab, int count): Debug.LogError and return if prefab is null or its GetComponent<T>() is null. Create a root transform (new GameObject named "Pool_" + typeof(T).Name) to parent pooled instances. Instantiate count instances, deactivate each, push onto an internal Stack<T>.\n- public T Get(): pop from the stack, skipping destroyed/null entries defensively; if empty, instantiate a fresh instance from the prefab (dynamic growth). Activate before returning. Debug.LogError and return null if Init was never called.\n- public void Release(T item): null guard; double-release guard (track a HashSet<T> of items currently pooled); deactivate, reparent under the pool root, push back.\n\nNo game-specific logic (no Item, no grid types). Brief XML doc comments on the class and Init.\n\nCommit message (first line):\nfeat: generic IPool<T> and MonoBehaviourPool<T>', { label: '3.2 IPool<T>', phase: 'IPool', schema: REPORT })
if (!pool || !pool.ok) return { aborted: 'Phase 3.2 failed or stopped', audit, pool }
log('3.2 done: ' + pool.summary)

phase('Pool Gems')
log('Phase 3.3: ItemPoolManager + rewiring')
const gems = await agent(CTX + '\n\nYOUR PHASE - 3.3 Pool the gem Item. Reports from earlier phases (already committed):\n' + JSON.stringify({ phase31: audit, phase32: pool }) + '\n\nTasks:\n\n1. Assets/_Project/Scripts/Core/Item.cs: add public void ResetForPool() that resets pooled state: Tier -> 0, GemData -> null, spriteRenderer.sprite -> null, spriteRenderer.color -> Color.white, tierLabel.text -> "" (null-guard the label).\n\n2. New file Assets/_Project/Scripts/Core/ItemPoolManager.cs - a MonoBehaviour:\n- [SerializeField] private GameObject itemPrefab; [SerializeField] private int prewarmCount = 32;\n- private IPool<Item> _pool; (the assignment requires the manager to OWN an IPool<T> field) - created in Awake() as a MonoBehaviourPool<Item> and Init(itemPrefab, prewarmCount). Debug.LogError if itemPrefab is null.\n- public Item Get(Vector3 position): take from pool, set transform.position, return (null-safe).\n- public void Release(Item item): call item.ResetForPool(), then _pool.Release(item).\n- Top-of-file "// POOL DESIGN:" comment block that explicitly covers all four required points: WHAT is pooled (gem Item - one spawned per move, two despawned + one spawned per merge); POOL SIZE 32 with reasoning (5x5 grid = 25 cells max on board plus transient churn, rounded up; larger LevelData grids covered by growth); STATIC OR DYNAMIC: dynamic, and why; RESET ON RELEASE: name the fields reset in Item.ResetForPool() (must be at least 3).\n\n3. Rewire Assets/_Project/Scripts/Core/GridManager.cs so gameplay never calls Instantiate/Destroy for Items:\n- Add a serialized ItemPoolManager reference (CLAUDE.md style).\n- SpawnItem(): use the pool manager Get(cell.transform.position) instead of Instantiate. If the reference is null, Debug.LogError once and fall back to the old Instantiate path so the game still runs unwired.\n- DespawnItem(): in play mode Release to the pool (fallback Destroy when unwired). Keep the edit-mode SafeDestroy path unchanged.\n- Keep the _liveItems bookkeeping from phase 3.1 correct in both paths.\nMergeManager already routes through GridManager after phase 3.1 - verify that with a grep; do not add pool calls there.\n\n4. Scene wiring - attempt via Unity MCP tools (load with ToolSearch: "select:mcp__ai-game-developer__assets-refresh,mcp__ai-game-developer__gameobject-find,mcp__ai-game-developer__gameobject-component-add,mcp__ai-game-developer__gameobject-component-modify,mcp__ai-game-developer__assets-find,mcp__ai-game-developer__scene-save"):\n- assets-refresh first so the new scripts compile.\n- Find the scene GameObject holding the GridManager component in the open mainGame scene.\n- Add the ItemPoolManager component to that GameObject; assign its itemPrefab to Assets/_Project/Prefabs/Item.prefab (locate via assets-find) and prewarmCount 32.\n- Assign GridManager\'s new pool manager reference to that component; scene-save.\nIf the editor/MCP is unreachable, skip wiring and write exact manual wiring steps in notes. Do NOT hand-edit the .unity file under any circumstances.\n\nCommit message (first line):\nfeat: pool gem items via ItemPoolManager', { label: '3.3 pool gems', phase: 'Pool Gems', schema: REPORT })
if (!gems || !gems.ok) return { aborted: 'Phase 3.3 failed or stopped', audit, pool, gems }
log('3.3 done: ' + gems.summary)

phase('Verify')
log('Verifying against the assignment checklist')
const CHECKCTX = 'You are a READ-ONLY reviewer of repo "' + REPO + '" on branch ' + BRANCH + '. Inspect with git -C "' + REPO + '" diff main...HEAD, git log, grep, and by reading files. Modify NOTHING; run no state-changing commands (git add/commit/checkout are forbidden). pass=true only if EVERY item below is satisfied; otherwise list each unsatisfied or uncertain item as a problem string with file:line references.\n\n'
const checks = await parallel([
  () => agent(CHECKCTX + 'Assignment Part 1 - Cache Audit:\n- At least two caching problems fixed, each with: a private field, assignment in Awake/Start, all usages replaced. Expected fixes: cached Camera in InputHandler; FindObjectsByType removed from the play-mode path of GridManager.ClearGrid (edit-mode fallback is acceptable); shared static square sprite in Cell; TryGetComponent in InputHandler pointer handlers.\n- A "// CACHE AUDIT" comment block sits at the top of EACH fixed script listing what was fixed and where.\n- MergeManager routes item create/destroy through GridManager (no direct Instantiate/Destroy of Items in MergeManager).', { label: 'verify:part1-cache', phase: 'Verify', schema: VERDICT }),
  () => agent(CHECKCTX + 'Assignment Part 2 - IPool<T>:\n- Assets/_Project/Scripts/Core/Pooling/IPool.cs exists and the interface is exactly: public interface IPool<T> { T Get(); void Release(T item); }\n- MonoBehaviourPool<T> implements IPool<T> with the "where T : Component" constraint.\n- Init(GameObject prefab, int count), Get() and Release(T item) are implemented CORRECTLY - actively look for logic bugs: prewarmed instances deactivated and parented; Get grows the pool when empty and activates the instance; Release deactivates, reparents and guards against double-release; null/uninitialized handling logs errors instead of throwing.', { label: 'verify:part2-ipool', phase: 'Verify', schema: VERDICT }),
  () => agent(CHECKCTX + 'Assignment Part 3 - Pool your own object:\n- A manager class (ItemPoolManager) creates and OWNS a field of type IPool<Item>.\n- Gameplay no longer calls Instantiate/Destroy for Items: run grep -n "Instantiate\\|Destroy(" over "' + REPO + '/Assets/_Project/Scripts" and confirm every remaining hit is a legitimate non-Item path (cell creation in CreateGrid, edit-mode SafeDestroy fallback, pool internals, GameManager singleton guard, the explicit unwired-fallback path with its LogError). Any unexplained Item Instantiate/Destroy is a problem.\n- A "// POOL DESIGN:" comment block exists in ItemPoolManager.cs and covers all four: what is pooled, pool size with reasoning, static-vs-dynamic choice, and at least 3 fields reset on Release.\n- Item.ResetForPool() resets at least 3 fields.', { label: 'verify:part3-pooled', phase: 'Verify', schema: VERDICT }),
  () => agent(CHECKCTX + 'Compile & Unity hygiene check:\n- Try the Unity MCP route first: ToolSearch "select:mcp__ai-game-developer__assets-refresh,mcp__ai-game-developer__console-get-logs", call assets-refresh (waits for compilation), then console-get-logs filtered to errors. Fail with the error text if any compile error mentions the changed/new scripts.\n- Also confirm every new .cs file and new folder under Assets/_Project/Scripts has a committed .meta file.\n- If the editor/MCP is unreachable: statically review every changed/new .cs file for compile errors (missing using directives such as System.Collections.Generic or UnityEngine, signature mismatches, typos) and report missing .meta files as a problem (they must be generated and committed before the PR).', { label: 'verify:compile', phase: 'Verify', schema: VERDICT }),
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
  fix = await agent(CTX + '\n\nYOUR PHASE - Fix round. Reviewers found these problems in the last three commits on this branch:\n- ' + problems.join('\n- ') + '\n\nPhase reports for context: ' + JSON.stringify({ audit, pool, gems }) + '\n\nFix every code-level problem. Problems that strictly require the Unity editor (scene wiring, .meta generation) when MCP is unreachable: leave them and list them in notes as user follow-ups instead. Reread your diff before committing.\n\nCommit message (first line):\nfix: address lesson 3 review findings', { label: 'fix findings', phase: 'Fix', schema: REPORT })
  log(fix ? 'Fix round done: ' + fix.summary : 'Fix agent returned nothing')
} else {
  log('No fix round needed')
}

return {
  branch: BRANCH,
  phases: { cacheAudit: audit, ipool: pool, poolGems: gems },
  problemsFound: problems,
  fixRound: fix,
  userFollowUps: [audit, pool, gems, fix].filter(Boolean).map(r => r.notes).filter(n => n && n.length > 0),
}
