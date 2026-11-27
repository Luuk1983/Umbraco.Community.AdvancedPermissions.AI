// Dutch (nl) localization for the Advanced Permissions AI copilot tools.
//
// See en.js for an explanation of how Umbraco AI derives these keys from the
// tool ids (`uaiTool_{camelCaseId}Label` / `...Description`, one pair per tool:
// uap_explain_access, uap_explain_library_access, uap_audit_permissions), the
// read scope id
// (`uaiToolScope_advancedPermissions:read{Label,Description}`) and the scope
// domain heading (`uaiToolScopeDomain_advancedPermissions`).
export default {
  uaiTool: {
    uapExplainAccessLabel: 'Toegang uitleggen',
    uapExplainAccessDescription:
      'Leg uit wie wat mag doen op een inhoudsitem — en waarom — inclusief alleen-lezen / niet-kunnen-verwijderen diagnoses en welke documenttypes hier aangemaakt mogen worden.',
    uapExplainLibraryAccessLabel: 'Bibliotheektoegang uitleggen',
    uapExplainLibraryAccessDescription:
      'Leg uit wie wat mag doen met een bibliotheekitem of -map — en waarom — en welke elementtypes in de bibliotheek aangemaakt mogen worden.',
    uapAuditPermissionsLabel: 'Permissies controleren',
    uapAuditPermissionsDescription:
      "Controleer de permissie-instellingen van een gebruikersgroep, een tak of de hele site op risico's en conflicten, in de inhoudsboom of de bibliotheek.",
  },
  uaiToolScope: {
    'advancedPermissions:readLabel': 'Geavanceerde permissies (lezen)',
    'advancedPermissions:readDescription':
      'Alleen-lezen toegang om geavanceerde permissies op te vragen en te controleren.',
  },
  uaiToolScopeDomain: {
    advancedPermissions: 'Geavanceerde permissies',
  },
};
