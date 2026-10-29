export const c4Navigation=[
 {label:"Proposals",href:"/component-04/assessments",icon:"",primary:true},
 {label:"Early Governance",href:"/component-04/assessments/early",icon:""},
 {label:"Regulatory Screening",href:"/component-04/assessments/regulatory",icon:""},
 {label:"Conflict Detection",href:"/component-04/assessments/conflict",icon:""},
 {label:"Institutional Workflow Preview",href:"/component-04/institutional-workflow",icon:""},
 {label:"Post-Workflow Anomaly",href:"/component-04/post-workflow",icon:""},
 {label:"Complaint Inbox",href:"/component-04/complaint-inbox",icon:""}
];
export function c4NavigationFor(isCommissioner:boolean){return c4Navigation.map(item=>item.label==="Complaint Inbox"&&isCommissioner?{...item,href:"/component-04/central-complaints"}:item);}
