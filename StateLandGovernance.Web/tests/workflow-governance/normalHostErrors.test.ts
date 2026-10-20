import assert from 'node:assert/strict';
import test from 'node:test';
import { parseError } from '../../src/lib/apiClient';
test('401 and empty 403 provide actionable access messages',async()=>{
 assert.match((await parseError(new Response('',{status:401}))).message,/Sign in/);
 assert.match((await parseError(new Response('',{status:403}))).message,/assignment/);
});
test('500 exposes safe reference and never exception content',async()=>{
 const error=await parseError(new Response(JSON.stringify({detail:'secret exception content',traceId:'safe-ref-123'}),{status:500}));
 assert.match(error.message,/safe-ref-123/); assert.doesNotMatch(error.message,/secret/);
});
test('conflict preserves the prerequisite action returned by the API',async()=>{
 assert.match((await parseError(new Response(JSON.stringify({detail:'Publish the reviewed snapshot first.'}),{status:409}))).message,/snapshot/);
});
