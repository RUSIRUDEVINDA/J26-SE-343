import {KiwulaAssessmentWorkspace} from "@/modules/component-04/components/KiwulaAssessmentWorkspace";
export default async function Page({params}:{params:Promise<{id:string}>}){const {id}=await params;return <KiwulaAssessmentWorkspace id={id}/>;}
