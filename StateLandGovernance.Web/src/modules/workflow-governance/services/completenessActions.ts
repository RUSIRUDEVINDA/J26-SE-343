export type ClassificationReviewRow={classifiedDocumentId:string;decision:string};
export function classificationNeedsReview(id:string,status:string,reviews:ClassificationReviewRow[]) {
 const decision=reviews.filter(r=>r.classifiedDocumentId===id).at(-1)?.decision;
 return status!=="Accepted" && decision!=="Confirmed" && decision!=="Corrected";
}
