import test from 'node:test';
import assert from 'node:assert/strict';
import {findingGroup, findingTone, findingStatusLabel} from '../src/modules/component-04/services/reportPresentation';
test('Catalogue statuses remain distinct from satisfied findings',()=>{
 assert.equal(findingGroup({status:'NotApplicable',isBlocking:false,requiresHumanReview:false}),'Not applicable');
 assert.equal(findingGroup({status:'InsufficientInformation',isBlocking:false,requiresHumanReview:false}),'Review items');
});

test('Unresolved and inapplicable findings never receive a success tone',()=>{
 const flags={isBlocking:false,requiresHumanReview:false};
 assert.equal(findingTone({...flags,status:'InsufficientInformation'}),'warning');
 assert.equal(findingStatusLabel('InsufficientInformation'),'Insufficient information ? unresolved');
 assert.equal(findingTone({...flags,status:'NotApplicable'}),'neutral');
 assert.equal(findingStatusLabel('NotApplicable'),'Not applicable');
});
test('Returned blocking flags remain visible even with an applicability status',()=>{
 const finding={isBlocking:true,requiresHumanReview:false,status:'NotApplicable'};
 assert.equal(findingGroup(finding),'Issues');
 assert.equal(findingTone(finding),'danger');
});
