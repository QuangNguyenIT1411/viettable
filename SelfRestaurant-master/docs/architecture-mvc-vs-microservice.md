# MVC to Microservice Web Migration Specification

## 1. Purpose
This document defines the mandatory implementation rules for migrating an existing **MVC web system** into the current **microservice-based web system**.

The target outcome is:
- Copy **functionality** from MVC into microservice
- Copy **UI/UX and visual appearance** from MVC into microservice
- Match MVC as closely as possible, with a target similarity of **99% to 100%**
- Implement **web only**
- **Do not** implement mobile app, desktop app, or any non-web client

This document must be treated as the **single source of truth** for all migration work.

---

## 2. Core Objective
You must analyze both codebases:
1. The current **microservice** project
2. The existing **MVC** project

Then implement on the **microservice web system** all missing or different behavior so that it reproduces the MVC system as accurately as possible.

The migration must preserve:
- business logic
- screen flow
- validation rules
- permissions/roles
- error handling
- success handling
- UI layout and behavior
- user experience

If the microservice architecture requires internal technical adaptation, you may adapt the implementation internally, but the **final user-facing result must remain equivalent to MVC**.

---

## 3. Mandatory Scope
### Included
- Web pages
- Web components
- Backend integration required for web
- APIs required for web flow
- Forms, tables, detail screens, search, filter, pagination
- Authentication/authorization behavior used by web
- Notifications, toast messages, modals, popup dialogs
- Validation, loading states, empty states, error states
- Routing/navigation behavior
- Responsive behavior only to the extent supported by the MVC version

### Excluded
- Mobile app
- Native app
- Desktop app
- Features not used by the MVC web version
- Unrelated refactors that change behavior or UI unnecessarily

---

## 4. Non-Negotiable Rules
1. **Web only**. Do not build app/mobile flows.
2. **MVC is the reference standard** for both behavior and interface.
3. The result must be **visually and functionally identical or near-identical**.
4. Do **not** redesign UI.
5. Do **not** simplify flows.
6. Do **not** replace MVC behavior with a “better” or “cleaner” alternative unless technically required.
7. Do **not** introduce breaking changes to the existing microservice architecture.
8. If any deviation is unavoidable, you must:
   - keep it minimal
   - document it clearly
   - explain the technical reason
9. Preserve all business rules from MVC.
10. Preserve all user-visible text, labels, messages, warnings, placeholders, and button actions as closely as possible.

---

## 5. Expected Similarity Standard
The migrated microservice web must match MVC in the following dimensions:

### 5.1 UI Visual Match
Replicate as closely as possible:
- page layout
- component structure
- spacing
- alignment
- font size
- font weight
- colors
- borders
- border radius
- shadows
- icons
- button styles
- input styles
- table styles
- modal/dialog appearance
- tab appearance
- card layout
- status badge style
- empty state design
- loading indicators
- tooltip behavior
- animation/transition behavior if present on MVC

### 5.2 UX / Interaction Match
Replicate as closely as possible:
- click flow
- navigation flow
- field focus behavior
- default values
- button enable/disable conditions
- validation timing
- blur/change/submit behavior
- confirmation dialogs
- error display position and wording
- loading timing and indicators
- pagination behavior
- filtering behavior
- sorting behavior
- search behavior
- state preservation when navigating back

### 5.3 Functional Match
Replicate as closely as possible:
- API behavior expected by UI
- data mapping
- business rules
- role-based visibility
- permission checks
- CRUD flow
- field dependencies
- conditional rendering
- conditional validation
- import/export behavior if present in MVC web
- attachment/file handling if present in MVC web

---

## 6. Implementation Workflow
For every module/screen, follow this exact order.

### Step 1: Analyze MVC
Identify and document:
- pages/screens in the module
- route structure
- user flow
- business logic
- validation rules
- role/permission behavior
- API usage or backend interaction
- UI components and states
- edge cases
- error cases
- success cases

### Step 2: Compare With Microservice
Determine:
- what already exists
- what is partially implemented
- what is missing
- what behaves differently
- what looks different
- what data mapping differs
- what validations differ
- what permissions differ

### Step 3: Implement Missing Backend/Integration
Add or adjust:
- API endpoints
- request/response mapping
- service integration
- validation handling
- error mapping
- authorization behavior
- data transformation

### Step 4: Implement / Adjust Frontend
Build or modify screens/components so they match MVC in:
- structure
- style
- behavior
- text
- user interaction
- state handling

### Step 5: Compare Against MVC Again
Perform a strict comparison for:
- layout
- spacing
- text
- button actions
- validation
- modal behavior
- table behavior
- search/filter/pagination
- response handling
- permission handling
- loading and empty states

### Step 6: Finalize With Difference Report
If there is any deviation from MVC, list it explicitly.
Do not hide differences.

---

## 7. Per-Module Deliverables
For each module, you must produce:

1. **Implemented code** in microservice
2. **List of migrated screens/features**
3. **List of API/backend changes**
4. **List of differences from MVC**, if any
5. **Reason for each difference**
6. **Risk/impact note**, if relevant

---

## 8. Required Comparison Checklist
Before marking a module complete, verify all items below.

### 8.1 Screen Checklist
- Route matches MVC flow
- Screen structure matches MVC
- Content order matches MVC
- Labels/text match MVC
- Buttons/actions match MVC
- Modal/popup behavior matches MVC
- Empty state matches MVC
- Error state matches MVC
- Loading state matches MVC

### 8.2 Form Checklist
- Field list matches MVC
- Field order matches MVC
- Default values match MVC
- Placeholder text matches MVC
- Validation rules match MVC
- Validation messages match MVC
- Required/optional logic matches MVC
- Submit flow matches MVC
- Success/failure behavior matches MVC

### 8.3 Table/List Checklist
- Columns match MVC
- Column order matches MVC
- Formatting matches MVC
- Pagination matches MVC
- Search matches MVC
- Filter matches MVC
- Sorting matches MVC
- Row action buttons match MVC
- Detail/open behavior matches MVC

### 8.4 Permission Checklist
- Roles match MVC behavior
- Hidden/disabled controls match MVC
- Unauthorized actions behave like MVC
- Protected routes/pages behave like MVC

### 8.5 Visual Checklist
- Margin/padding aligned with MVC
- Font sizes match MVC
- Font weights match MVC
- Colors match MVC
- Icons match MVC
- Button height/width/style match MVC
- Input style matches MVC
- Table style matches MVC
- Modal style matches MVC
- Responsive behavior matches MVC scope

### 8.6 Interaction Checklist
- Click sequence matches MVC
- Validation timing matches MVC
- Navigation result matches MVC
- Back/cancel behavior matches MVC
- Toast/alert messages match MVC
- Loading trigger timing matches MVC
- Disable state logic matches MVC

---

## 9. Constraints on Code Changes
### Allowed
- Refactor internally only when required to fit microservice architecture
- Add adapters/mappers/services to preserve MVC-compatible output
- Reorganize implementation details internally if behavior remains equivalent

### Not Allowed
- Replacing MVC flow with a new product decision
- Changing labels/messages without reason
- Redesigning layout
- Skipping edge cases
- Ignoring validation details
- Ignoring permission details
- Implementing only “main flow” while skipping secondary behavior
- Making UI “approximately similar” without exact comparison

---

## 10. Handling Technical Gaps
If the MVC system relies on a pattern that does not exist yet in microservice:
1. Recreate the same user-facing behavior in a microservice-compatible way
2. Keep naming and mapping consistent where practical
3. Preserve output and flow compatibility
4. Document the adaptation clearly

If exact reproduction is impossible due to architectural constraints, provide:
- what cannot be matched exactly
- why
- what substitute was implemented
- how close it is to MVC

---

## 11. Work Style Requirements
- Work **module by module**
- Complete one area thoroughly before moving to the next
- Re-check MVC after each implementation
- Prefer correctness and fidelity over speed
- Do not assume existing microservice behavior is correct if MVC differs
- Always resolve conflicts in favor of MVC unless impossible

---

## 12. Output Format for Every Task
For each request, respond in this structure:

### A. Scope
- Module/screen being migrated

### B. MVC Findings
- Key behavior
- Key UI elements
- Key validations
- Key permissions

### C. Microservice Changes
- Backend/API changes
- Frontend/UI changes
- Mapping/integration changes

### D. Validation Result
- What was checked against MVC

### E. Remaining Differences
- Explicit list of remaining differences
- Reason for each difference

If there are no differences, explicitly say:
**“No remaining differences identified against MVC for this scope.”**

---

## 13. Instruction Priority
When conflicts happen, follow this priority order:
1. This specification document
2. MVC implementation behavior and interface
3. Existing microservice conventions
4. Optional internal optimizations

---

## 14. Final Acceptance Criteria
A module is only considered complete if all conditions are met:
- Functionally equivalent to MVC
- Visually equivalent to MVC
- Validation equivalent to MVC
- Permission behavior equivalent to MVC
- Error/success handling equivalent to MVC
- No undocumented deviation remains
- Works on web only
- Does not break microservice architecture

---

## 15. Execution Instruction
Read this entire file before starting any implementation.
Then:
1. analyze MVC for the requested module
2. compare with microservice
3. implement missing pieces
4. verify screen-by-screen and flow-by-flow against MVC
5. report remaining differences explicitly

Never assume partial similarity is acceptable.
Target result must be as close to MVC as possible: **99% to 100% match**.

