export const investigationLeaveEvent="c4-investigation-before-leave";
export function requestInvestigationLeave(){return window.dispatchEvent(new Event(investigationLeaveEvent,{cancelable:true}));}
