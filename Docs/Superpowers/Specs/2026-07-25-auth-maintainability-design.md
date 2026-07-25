# Auth Maintainability Design

## Goal

Improve the readability, ownership boundaries, and interview explainability of the existing Firebase Auth, Firestore session, and Fusion entry flow without changing its observable login, registration, profile, scene-transition, or single-login behavior.

## Baseline

- `AuthUIView` is 653 lines and combines Unity UI binding, form state, Firebase initialization, login and registration orchestration, profile checks, and scene transition.
- `AuthController.CurrentUser` duplicates the process-wide `UserSession.Current` state. The review found no consumer for `CurrentUser`.
- `LoginMenu` has no missing scripts. `AuthUIView` is attached to `Canvas` and currently resolves most references through hierarchy paths at runtime.
- This iteration does not modify Firebase credentials, Firestore document fields, session-lease behavior, Fusion startup, or scene hierarchy.

## Scope

### Included

- Extract focused UI binding and presentation helpers from `AuthUIView`.
- Extract the successful-login continuation into a coordinator with a single responsibility: profile readiness followed by scene entry.
- Remove the unused `AuthController.CurrentUser` duplicate state while keeping `UserSession` as the current runtime source.
- Apply consistent names, XML comments, and short rationale comments at Unity lifecycle and async boundaries.
- Add EditMode coverage for deterministic validation and mapping behavior where the existing Unity/Firebase dependencies allow it.
- Validate compilation and the affected scene through Unity MCP.

### Excluded

- Firebase ID token to Photon Custom Authentication integration.
- Firestore security rules and Profile/Session document split.
- Photon reconnection or session lease semantics.
- Changing serialized scene references, prefabs, button wiring, or Firebase/Fusion behavior.

## Architecture

```text
AuthUIView (MonoBehaviour)
    -> AuthFormBindings: read inputs, clear fields, show field/status feedback
    -> AuthController: login, registration, sign-out use cases
    -> AuthLoginFlowCoordinator: after successful login, check profile then request Play scene
    -> UsernamePanelView / GameSceneController: existing behavior preserved

AuthController
    -> FirebaseAuthManager + FirestoreAuthSessionRepository
    -> UserSession: sole in-memory session state
```

`AuthUIView` remains the Unity lifecycle and event adapter, so existing scene wiring stays valid. Extracted classes must not inherit `MonoBehaviour`, search Unity hierarchy, or call Firebase/Fusion APIs unless that responsibility already belongs to their existing dependency.

## Data And Error Flow

1. `AuthUIView` reads the current login form values and delegates to `AuthController.LoginAsync`.
2. On an authentication failure, the View maps `AuthField` to the existing field-tip or status controls.
3. On success, `AuthLoginFlowCoordinator` delegates to the existing profile check and only then requests `GameSceneController.LoadPlaySceneAsync`.
4. Existing loading overlay and submit-lock behavior remains at the Unity UI boundary. Async exceptions must be caught at that boundary and rendered through the existing status text.
5. `AuthController` writes only `UserSession`; it no longer maintains a second user copy.

## File Ownership

| File | Responsibility |
| --- | --- |
| `Assets/FireBase+Photon/Scripts/Auth/AuthUIView.cs` | Unity lifecycle, button events, submit lock, composing focused collaborators. |
| `Assets/FireBase+Photon/Scripts/Auth/AuthFormBindings.cs` | Serialized/non-serialized UI references, form value collection, panel state, and field/status rendering. |
| `Assets/FireBase+Photon/Scripts/Auth/AuthLoginFlowCoordinator.cs` | Profile-ready to Play-scene continuation with error reporting callback. |
| `Assets/FireBase+Photon/Scripts/Auth/AuthController.cs` | Authentication use cases and authoritative `UserSession` updates. |
| `Assets/FireBase+Photon/Tests/EditMode/AuthControllerValidationTests.cs` | Deterministic login and registration request validation coverage. |

## Acceptance Criteria

- `AuthUIView` no longer contains profile-to-scene orchestration or low-level input-tip rendering helpers.
- `AuthController` has no `CurrentUser` property or assignment, and all current consumers still use `UserSession`.
- Public classes and lifecycle/async boundaries explain their responsibility and non-obvious constraints in Chinese XML or rationale comments.
- Login and registration validation results retain their current messages and target fields.
- Unity MCP compilation completes without project-script errors.
- Unity MCP confirms `LoginMenu` still has no missing scripts after the change.

## Interview Narrative

The login page uses a thin Unity View that owns scene events and visual state. Authentication rules live in `AuthController`, and the login continuation lives in a dedicated coordinator that waits for the profile before entering the network scene. `UserSession` is the single in-memory runtime session source. This makes the Firebase, Firestore, and Fusion boundaries explicit without claiming that the current client-side sequence is server-side authentication.

## Risks And Mitigations

- Runtime path binding is fragile: preserve the current paths in this iteration and isolate them behind one binding class instead of renaming scene nodes.
- Unity events require `void` handlers: retain `async void` only at button/lifecycle boundaries; move reusable work to `Task`-returning methods.
- Scene changes can invalidate UI objects after an await: keep scene-entry exception handling at the View boundary and avoid callbacks that access destroyed controls.
