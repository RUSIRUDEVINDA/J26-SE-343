import {apiGet, apiPost} from "@/lib/apiClient";
import type {HandlingState,HandlingView} from "./complaintHandling";
import type {Complaint, ComplaintDetail} from "./registeredComplaints";
export type Assignment = {id:string;complaintId:string;revision:number;targetActorId:string;targetOfficeCode:string;assignedAtUtc:string;reason:string};
export type Officer = {actorId:string;displayName:string;officeCode:string};
export type CentralItem = {complaint:Pick<Complaint,"id"|"subject"|"submittedAtUtc"|"classificationState"|"reviewStatus">;assignment:Assignment|null;targetEligible:boolean;handling:HandlingState};
export type CentralDetail = {detail:ComplaintDetail;assignment:Assignment|null;targetEligible:boolean;handling:HandlingView};
export type AssignmentInput = {expectedHandlingRevision:number;submissionKey:string;expectedAssignmentRevision:number;targetActorId:string;reason:string};
const base="/api/governance-intelligence/central-complaints";
export const centralComplaintsApi={
 capabilities:()=>apiGet<{canRead:boolean;canAssign:boolean}>(`${base}/capabilities`,{cache:"no-store"},"none"),
 queue:(handling:"unassigned"|"commissioner"|"ds"|"terminal",cursor?:string)=>apiGet<{items:CentralItem[];nextCursor:string|null}>(`${base}?handling=${handling}${cursor?`&cursor=${encodeURIComponent(cursor)}`:""}`,{cache:"no-store"},"none"),
 list:(assignment:"assigned"|"unassigned",cursor?:string)=>apiGet<{items:CentralItem[];nextCursor:string|null}>(`${base}?assignment=${assignment}${cursor?`&cursor=${encodeURIComponent(cursor)}`:""}`,{cache:"no-store"},"none"),
 detail:(id:string)=>apiGet<CentralDetail>(`${base}/${encodeURIComponent(id)}`,{cache:"no-store"},"none"),
 officers:()=>apiGet<Officer[]>(`${base}/officers`,{cache:"no-store"},"none"),
 history:(id:string)=>apiGet<Assignment[]>(`${base}/${encodeURIComponent(id)}/assignments`,{cache:"no-store"},"none"),
 assign:(id:string,input:AssignmentInput)=>apiPost<{event:Assignment;current:Assignment;reused:boolean}>(`${base}/${encodeURIComponent(id)}/assignments`,input,undefined,"none")
};
