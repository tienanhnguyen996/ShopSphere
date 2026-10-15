# Apply branch protection rules to main branch
$body = @'
{
  "required_status_checks": {
    "strict": true,
    "contexts": ["CI Status", "Tests Status", "Lint Status"]
  },
  "enforce_admins": false,
  "required_pull_request_reviews": {
    "required_approving_review_count": 1,
    "dismiss_stale_reviews": true
  },
  "restrictions": null,
  "allow_force_pushes": false,
  "allow_deletions": false
}
'@

$body | gh api repos/tienanhnguyen996/ShopSphere/branches/main/protection --method PUT --input -
