import assert from 'node:assert/strict';
import test from 'node:test';
import { assessmentState } from '../../src/modules/workflow-governance/services/assessmentState';
test('an absent assessment is never stale regardless of currentness',()=>{
 assert.equal(assessmentState(null,false),'NotAssessed'); assert.equal(assessmentState(undefined,true),'NotAssessed');
});
test('only an existing outdated assessment is stale',()=>{
 assert.equal(assessmentState({id:'previous'},false),'Stale'); assert.equal(assessmentState({id:'current'},true),'Current');
});
