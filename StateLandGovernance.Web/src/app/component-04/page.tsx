import {redirect} from "next/navigation";
import type {Metadata} from "next";

export const metadata:Metadata={title:"Governance Intelligence",description:"Governance assessments and advisory evidence for state land lease review."};

// Existing governance overview implementation is retained; only its default navigation is hidden.
export default function Component04Page(){redirect("/component-04/assessments");}
