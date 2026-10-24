import assert from 'node:assert/strict';import test from 'node:test';
import {classificationNeedsReview} from '../../src/modules/workflow-governance/services/completenessActions';
test('classification confirmation resolves an uncertain original without changing its history',()=>{
 assert.equal(classificationNeedsReview('doc','Uncertain',[]),true);
 assert.equal(classificationNeedsReview('doc','Uncertain',[{classifiedDocumentId:'doc',decision:'Confirmed'}]),false);
 assert.equal(classificationNeedsReview('doc','Uncertain',[{classifiedDocumentId:'doc',decision:'Corrected'}]),false);
 assert.equal(classificationNeedsReview('doc','Uncertain',[{classifiedDocumentId:'other',decision:'Confirmed'}]),true);
 assert.equal(classificationNeedsReview('doc','Accepted',[]),false);
 assert.equal(classificationNeedsReview('doc','Uncertain',[{classifiedDocumentId:'doc',decision:'Rejected'}]),true);
});
