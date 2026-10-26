# Nightshift eligibility filter. Input: the gh issue list JSON array.
# Output: one JSON object for the oldest eligible issue, or null.
#
# Eligibility, per docs/labels.md:
#   - has nightshift and agent-ready
#   - has none of: blocked, decision required, human-only,
#     in development, awaiting-review, awaiting-qa
#   - body is long enough to implement from (> 200 characters)
#   - no open pull request already closes it ($in_flight is newline separated)
[ .[]
  | (.labels | map(.name)) as $l
  | .number as $n
  | select($l | index("nightshift"))
  | select($l | index("agent-ready"))
  | select([$l[]
      | select(. == "blocked"
          or . == "decision required"
          or . == "human-only"
          or . == "in development"
          or . == "awaiting-review"
          or . == "awaiting-qa")]
      | length == 0)
  | select((.body // "") | length > 200)
  | select(([$in_flight | split("\n")[] | select(length > 0) | tonumber] | index($n)) | not)
]
| sort_by(.number)
| .[0] // null
| if . == null then null else
    {
      number: .number,
      title: .title,
      branch: ("\(.number)-" + (.title
        | ascii_downcase
        | gsub("[^a-z0-9]+"; "-")
        | gsub("^-+|-+$"; "")
        | .[0:30]
        | gsub("-+$"; "")))
    }
  end
