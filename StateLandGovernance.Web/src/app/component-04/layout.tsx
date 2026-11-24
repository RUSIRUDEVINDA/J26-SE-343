import {GovernanceSessionBoundary} from "@/modules/component-04/components/GovernanceSessionBoundary";
import type {ReactNode} from "react";
import {CentralComplaintLink} from "@/modules/component-04/components/CentralComplaints";
import {PageLayout} from "@/shared/components/PageLayout";
import {GovernanceWorkspace} from "@/modules/component-04/components/GovernanceWorkspace";
import {c4NavigationFor} from "@/modules/component-04/services/c4Navigation";
import {headers} from "next/headers";
import {redirect} from "next/navigation";
export default async function Component04Layout({children}:{children:ReactNode}){const trusted=await headers();if(trusted.get("x-c4-admitted")!=="true")redirect("/sign-in");const navigation=c4NavigationFor(trusted.get("x-c4-commissioner")==="true");return <GovernanceSessionBoundary><PageLayout compactPresentation navigationItems={navigation} navigationLabel="Governance Intelligence" navigationBasePath="/component-04" navigationCompactMobile><GovernanceWorkspace><CentralComplaintLink/>{children}</GovernanceWorkspace></PageLayout></GovernanceSessionBoundary>;}
