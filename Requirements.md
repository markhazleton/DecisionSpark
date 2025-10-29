
## 1. Purpose

We are adding a Dynamic Decision Routing engine (example: “Family Saturday Planner”) that asks for minimal info, then recommends one of several outcomes (Go Bowling / Go Golfing / Movie Night).

This engine must integrate with our conversation-style API surface using:

* `POST /start`
* `POST {next_url}` (e.g. `/v2/pub/conversation/{sessionId}/next` or similar internal pattern)

The client will:

1. Call `/start`
2. Read the response, including `question`, `texts`, `next_url`
3. POST answers to `next_url` repeatedly until completion

No client logic changes. All routing logic and next-step navigation is driven by the server’s responses.

---

## 2. High-Level Runtime Flow

### Step 1: Client calls `POST /start` `{}`

Server:

1. Creates a new decision session (`session_id`).
2. Loads the active `DecisionSpec` for this flow (e.g. `FAMILY_SATURDAY_V1`).
3. Runs evaluation:

   * If it can already determine an outcome -> prepare a completion response.
   * If not -> identify the next trait we need (e.g. `group_size`) and generate a question for it.

### Step 2: Server responds to `/start`

* If more info is needed:

  * Return a response with `question`, `texts`, and `next_url` (the URL to send the user’s answer).
* If the outcome is already known:

  * Return final recommendation (`is_complete: true`, `final_result`, etc.) and omit `next_url`.

### Step 3: Client POSTs user answer to `next_url`

* The body will be either:

  * `{ "user_input": "5 people total, ages 4, 9, 38, 40, and 12" }`
  * or `{ "selected_option_ids": [...], "selected_option_texts": [...] }` (if a future flow uses options)

Server:

1. Looks up the session.
2. Maps the answer to the trait we were waiting on.
3. Saves that trait into the session.
4. Re-runs evaluation.
5. Either returns final result, or returns the next question + a new `next_url`.

This repeats until `is_complete`.

---

## 3. Request/Response Contracts

### 3.1 `POST /start`

**Purpose:** Initialize a decision session and return either the first question or a final recommendation.

**Request:**

```http
POST /start HTTP/1.1
Content-Type: application/json
X-API-KEY: {apiKey}

{}
```

* Body: `{}` for now (we may extend later to allow initial known values like `group_size`).

**Server responsibilities:**

1. Create `session_id`.
2. Load the active DecisionSpec JSON from `/Config/DecisionSpecs/{specId}.{version}.active.json`.
3. Evaluate using any default/seed data.
4. Produce a response in the Conversation API-compatible shape below.

**Response if we still need info:**

```json
{
  "texts": [
    "Let's figure out the best plan for Saturday.",
    "I'll ask a couple quick questions."
  ],
  "question": {
    "id": "group_size",
    "source": "FAMILY_SATURDAY_V1",
    "text": "How many people are going on the outing?",
    "allow_free_text": true,
    "is_free_text": true,
    "allow_multi_select": false,
    "is_multi_select": false,
    "type": "text"
  },
  "next_url": "https://api.example.com/v2/pub/conversation/abc123/next"
}
```

**Response if we can already decide an outcome:**

```json
{
  "is_complete": true,
  "texts": [
    "Here's what I recommend for Saturday:"
  ],
  "display_cards": [
    {
      "title": "Bowling night 🎳",
      "subtitle": "Fun for everyone",
      "group_id": "activity_recommendation",
      "care_type_message": "Bowling is a great fit for your group.",
      "icon_url": "https://example.local/icons/bowling.png",
      "body_text": [
        "It's easy for mixed ages.",
        "You can handle a bigger group."
      ],
      "care_type_details": [
        "Look for family lanes or bumpers if you need them.",
        "Snack bar = instant dinner."
      ],
      "rules": [
        "Call ahead to reserve a lane if it's Saturday night."
      ]
    }
  ],
  "care_type_message": "Bowling is perfect for your crew — easy and fun for everyone.",
  "final_result": {
    "outcome_id": "GO_BOWLING",
    "resolution_button_label": "Reserve a lane",
    "resolution_button_url": "https://example.local/bowling",
    "analytics_resolution_code": "ROUTE_BOWLING"
  },
  "raw_response": "ROUTE_BOWLING"
}
```

Notes:

* Shape matches your documented `StartResponse` and `NextResponse` contracts.
* We may include `display_cards`, `care_type_message`, and `final_result` at this point if we’re done.
* When `is_complete` is true, we do not need to provide `next_url`.

---

### 3.2 `POST {next_url}`

**Purpose:** Continue the conversation/session with a user response.

`next_url` will typically look like:
`https://api.example.com/v2/pub/conversation/{sessionId}/next`

The client treats `next_url` as opaque and posts to it exactly as given.

**Request examples:**

Free-text answer:

```json
{
  "user_input": "There are 5 of us: 4, 9, 38, 40, 12"
}
```

or option-based answer (for multi-select style flows):

```json
{
  "selected_option_ids": [101, 203],
  "selected_option_texts": ["Fever", "Persistent cough"]
}
```

Server responsibilities:

1. Extract `{sessionId}` from the `next_url`.
2. Load the session state.
3. Determine which trait we were waiting on (e.g. `all_ages`).
4. Parse input into that trait.

   * e.g. "4, 9, 38" → `[4, 9, 38]`
   * Derive `min_age = 4`.
5. Store into session’s known trait set.
6. Re-run evaluation against the DecisionSpec.

**Response if still gathering info:**

```json
{
  "texts": [
    "Thanks. One more thing so I can dial this in."
  ],
  "question": {
    "id": "all_ages",
    "source": "FAMILY_SATURDAY_V1",
    "text": "What are the ages of everyone who's going? Please list ages like: 4, 9, 38.",
    "allow_free_text": true,
    "is_free_text": true,
    "allow_multi_select": false,
    "is_multi_select": false,
    "type": "text"
  },
  "next_url": "https://api.example.com/v2/pub/conversation/abc123/next",
  "prev_url": "https://api.example.com/v2/pub/conversation/abc123/prev"
}
```

**Response when decision can be made now:**

```json
{
  "is_complete": true,
  "texts": [
    "Got it. Here's what I recommend:"
  ],
  "display_cards": [
    {
      "title": "Movie night 🍿",
      "subtitle": "Stay in and relax",
      "group_id": "activity_recommendation",
      "care_type_message": "Movie night sounds perfect — blankets, snacks, everyone can chill.",
      "icon_url": "https://example.local/icons/movie.png",
      "body_text": [
        "Young kids can participate without getting overtired.",
        "You control the pace and environment."
      ],
      "care_type_details": [
        "Let everyone vote on the movie.",
        "Make popcorn or cocoa."
      ],
      "rules": [
        "Pick something age-appropriate for the youngest kid."
      ]
    }
  ],
  "care_type_message": "Movie night is the best fit for your group.",
  "final_result": {
    "outcome_id": "MOVIE_NIGHT",
    "resolution_button_label": "Pick a movie",
    "resolution_button_url": "https://example.local/movies",
    "analytics_resolution_code": "ROUTE_MOVIE"
  },
  "raw_response": "ROUTE_MOVIE"
}
```

No `next_url` is returned, per your existing contract, because we have completed routing.

---

## 4. Internal Engine Responsibilities

Even though the public runtime surface is just `/start` and `{next_url}`, internally we still need the same engine pieces.

### 4.1 DecisionSpec Loader

* Reads the active JSON config from disk:

  * `/Config/DecisionSpecs/FAMILY_SATURDAY_V1.0.0.active.json`
* Deserializes into C# models:

  * `DecisionSpec`
  * `TraitQuestion`
  * `OutcomeDefinition`
  * `TraitRule`

### 4.2 Session Store

* On `/start`, create a session:

  ```json
  {
    "session_id": "abc123",
    "spec_id": "FAMILY_SATURDAY_V1",
    "version": "1.0.0",
    "known_traits": {},
    "awaiting_trait_key": null,
    "is_complete": false
  }
  ```
* On subsequent `{next_url}` calls, retrieve and update this object.

For POC: in-memory dictionary keyed by `session_id`.
For production: Redis or similar.

### 4.3 RoutingEvaluator

* Input: `DecisionSpec`, `known_traits`
* Output:

  * Either:

    ```json
    {
      "is_complete": true,
      "outcome": { ...mapped OutcomeDefinition... }
    }
    ```
  * Or:

    ```json
    {
      "is_complete": false,
      "next_trait_key": "all_ages",
      "question_text": "What are the ages of everyone who's going?",
      "answer_type": "text",
      "parse_hint": "Return list of integers, e.g. [4, 9, 38]."
    }
    ```

Rules:

* Evaluate `immediate_select_if` first (e.g. if `min_age < 5` → Movie Night immediately).
* Evaluate `decision_rules`.
* If exactly one outcome is satisfied → return that.
* If multiple are still viable → choose which missing trait would best disambiguate.
* Derived traits like `min_age` are computed on the fly from collected traits (e.g. `all_ages`).

### 4.4 QuestionGeneratorService (OpenAI)

* Input:

  * `DecisionSpec.safety_preamble`
  * `TraitQuestion.question_text`
* Output:

  * A single friendly, compliant question string.
* This becomes `question.text` in API responses.
* Note: OpenAI is only used for phrasing, not routing logic.

---

## 5. How We Respond in Conversation API Terms

We now have the mapping from engine output → API response:

### Engine says “I need a trait”

We respond with:

* `texts`: optional explanation lines
* `question`: block following the documented `Question` shape
* `next_url`: absolute URL telling client what to call next

The `question.id` should match the trait key we’re collecting (`group_size`, `all_ages`, etc.).
That gives the client a stable ID, and on the next POST to `next_url`, we know what to expect.

### Engine says “I have a final outcome”

We respond with:

* `is_complete: true`
* `final_result`: maps outcome metadata
* `display_cards`: presentational card(s)
* `care_type_message`: summary / headline
* No `next_url`

This matches the `NextResponse` contract in your Conversation API.

---

## 6. What Changed (from prior draft)

* The entry route is now `POST /start` (not `/v2/pub/start`).
* We are still honoring the `Question`, `NextResponse`, `display_cards`, and `final_result` shapes from your existing Conversation API documentation.
* HATEOAS-style navigation is preserved as `next_url` and `prev_url` in responses, and the client still obeys those URLs exactly.
* The config-driven routing engine (DecisionSpec) and the session/evaluator logic sit behind `/start` and `{next_url}` — no additional public endpoints are required.

---

This gives engineering everything they need to scaffold:

* `/start` controller
* `/conversation/{sessionId}/next` controller
* Session store
* Spec loader
* Evaluator
* OpenAI question phrasing
* Response mappers to the Conversation API schema

All consistent with your updated route contract.
