import assert from 'node:assert/strict';
import test from 'node:test';
import {readDevelopmentSession,clearDevelopmentSession,signInToPlatform,signOutDevelopment,developmentApiBase} from '../../src/lib/developmentSession';
test('Development session expiry clears stored credentials',()=>{
 const old=globalThis.window;const storage=new Map<string,string>();
 Object.defineProperty(globalThis,'window',{configurable:true,value:{sessionStorage:{getItem:(k:string)=>storage.get(k)??null,setItem:(k:string,v:string)=>storage.set(k,v),removeItem:(k:string)=>storage.delete(k)},dispatchEvent:()=>true}});
 try{storage.set('slg.localDevelopmentSession',JSON.stringify({accessToken:'TEST-only',expiresAtUtc:new Date(Date.now()-1000).toISOString()}));assert.equal(readDevelopmentSession(),null);assert.equal(storage.size,0);
 storage.set('slg.localDevelopmentSession',JSON.stringify({accessToken:'TEST-only',expiresAtUtc:new Date(Date.now()+60000).toISOString()}));assert.equal(readDevelopmentSession()?.accessToken,'TEST-only');clearDevelopmentSession();assert.equal(readDevelopmentSession(),null);
 }finally{Object.defineProperty(globalThis,'window',{configurable:true,value:old});}
});

test('Normal login redirects to platform home while preserving the role profile and session',async()=>{
 const oldWindow=globalThis.window,oldFetch=globalThis.fetch;
 const storage=new Map<string,string>();
 Object.defineProperty(globalThis,'window',{configurable:true,value:{sessionStorage:{getItem:(k:string)=>storage.get(k)??null,setItem:(k:string,v:string)=>storage.set(k,v),removeItem:(k:string)=>storage.delete(k)},dispatchEvent:()=>true}});
 try {
  const session={accessToken:'UNIT-TEST-token',expiresAtUtc:new Date(Date.now()+60000).toISOString(),user:{home:'/workflow-governance/my-requests'}};
  globalThis.fetch=async(input,init)=>{
   assert.ok(String(input).endsWith('/api/auth/sign-in'));
   assert.deepEqual(JSON.parse(String(init?.body)),{username:'fictional',password:'UNIT-TEST-password'});
   return new Response(JSON.stringify(session),{status:200});
  };
  const destinations:string[]=[];
  assert.equal((await signInToPlatform('fictional','UNIT-TEST-password',path=>destinations.push(path))).user.home,'/workflow-governance/my-requests');
  assert.deepEqual(destinations,['/']);
  assert.equal(readDevelopmentSession()?.accessToken,'UNIT-TEST-token');
  globalThis.fetch=async(input,init)=>{
   assert.ok(String(input).endsWith('/api/auth/sign-out'));
   assert.equal(new Headers(init?.headers).get('Authorization'),'Bearer UNIT-TEST-token');
   return new Response(null,{status:204});
  };
  await signOutDevelopment(); assert.equal(readDevelopmentSession(),null);
  globalThis.fetch=async()=>new Response(null,{status:401});
  await assert.rejects(signInToPlatform('fictional','wrong',path=>destinations.push(path)),/Incorrect username or password/);
  assert.deepEqual(destinations,['/']); // A failed login must not navigate.
  assert.equal(readDevelopmentSession(),null);
  assert.ok(!developmentApiBase().includes('127.0.0.1'));
 } finally { globalThis.fetch=oldFetch;Object.defineProperty(globalThis,'window',{configurable:true,value:oldWindow}); }
});
