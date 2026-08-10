// Dutch (nl) localization for the Advanced Permissions AI copilot tools.
//
// See en.js for an explanation of how Umbraco AI derives these keys from the
// tool ids (`uaiTool_{camelCaseId}Label` / `...Description`, one pair per tool:
// uap_explain_access, uap_audit_permissions, uap_explain_concepts), the read scope id
// (`uaiToolScope_advancedPermissions:read{Label,Description}`) and the scope
// domain heading (`uaiToolScopeDomain_advancedPermissions`).
export default {
  uaiTool: {
    uapExplainAccessLabel: 'Toegang uitleggen',
    uapExplainAccessDescription:
      'Leg uit wie wat mag doen op een inhoudsitem — en waarom — inclusief alleen-lezen / niet-kunnen-verwijderen diagnoses en welke documenttypes hier aangemaakt mogen worden.',
    uapAuditPermissionsLabel: 'Permissies controleren',
    uapAuditPermissionsDescription:
      "Controleer de permissie-instellingen van een gebruikersgroep, een tak of de hele site op risico's en conflicten.",
    uapExplainConceptsLabel: 'Permissiebegrippen uitleggen',
    uapExplainConceptsDescription:
      'Leg uit hoe de permissies van deze site werken — Allow/Deny-vermeldingen, voorrang, bereik, overerving, Priority Override, Insert Options, en waar je een permissie aanpast in de backoffice.',
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
