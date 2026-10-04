// English localization for the Advanced Permissions AI copilot tools.
//
// Umbraco AI's "Select Tools" dialog localizes each tool through keys derived
// from the tool id: `uaiTool_{camelCaseId}Label` / `uaiTool_{camelCaseId}Description`
// (see Umbraco.AI.Web.StaticAssets app bundle). Umbraco's localization system
// splits a term on the FIRST underscore into a dictionary section and key, so
// `uaiTool_uapExplainAccessLabel` resolves to `uaiTool.uapExplainAccessLabel`.
//
// Tool id -> camelCase mapping (split on [-_.\s]+, first segment lowercased):
//   uap_explain_access         -> uapExplainAccess
//   uap_explain_library_access -> uapExplainLibraryAccess
//   uap_audit_permissions      -> uapAuditPermissions
//   uap_explain_concepts       -> uapExplainConcepts
//   uap_explain_editors        -> uapExplainEditors
//
// Scope id `advanced-permissions:read` -> `advancedPermissions:read` (the colon
// is NOT a separator and survives camelCasing), giving the quoted scope key below.
//
// Scope domain `advanced-permissions` -> `advancedPermissions`, surfaced as the
// heading key `uaiToolScopeDomain_advancedPermissions`.
export default {
  uaiTool: {
    uapExplainAccessLabel: 'Explain access',
    uapExplainAccessDescription:
      "Explain who can do what on a content node — and why — including read-only / can't-delete diagnoses and which document types can be created here.",
    uapExplainLibraryAccessLabel: 'Explain Library access',
    uapExplainLibraryAccessDescription:
      'Explain who can do what on a Library item or folder — and why — and which element types can be created in the Library.',
    uapAuditPermissionsLabel: 'Audit permissions',
    uapAuditPermissionsDescription:
      "Scan a user group's, a subtree's, or the whole site's permission setup for risks and conflicts, in the content tree or the Library.",
    uapExplainConceptsLabel: 'Explain permission concepts',
    uapExplainConceptsDescription:
      'Explain how this site’s permissions work — Allow/Deny entries, precedence, scopes, inheritance, Priority Override, and the create filters.',
    uapExplainEditorsLabel: 'Explain the permission editors',
    uapExplainEditorsDescription:
      'Explain which of the permission editors and viewers to use for a given question, how they differ, and where to find them in the backoffice.',
  },
  uaiToolScope: {
    'advancedPermissions:readLabel': 'Advanced Permissions (read)',
    'advancedPermissions:readDescription':
      'Read-only access to query and audit Advanced Permissions.',
  },
  uaiToolScopeDomain: {
    advancedPermissions: 'Advanced Permissions',
  },
};
