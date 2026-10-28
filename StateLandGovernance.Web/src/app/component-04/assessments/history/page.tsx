import {CombinedHistory} from "@/modules/component-04/components/CombinedHistory";
import {InvestigationHistory} from "@/modules/component-04/components/InvestigationHistory";
import {KiwulaAssessmentWorkspace} from "@/modules/component-04/components/KiwulaAssessmentWorkspace";
import {OfficerSavedReports} from "@/modules/component-04/components/OfficerSavedReports";
export default function Page(){return <><header><h1>Assessment History / Reports</h1><p>Saved results accessible to your account. Opening a report does not run an assessment.</p></header><InvestigationHistory/><OfficerSavedReports/><KiwulaAssessmentWorkspace historyOnly/><CombinedHistory/></>;}
