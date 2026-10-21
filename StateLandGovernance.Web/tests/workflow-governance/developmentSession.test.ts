import assert from 'node:assert/strict';
import test from 'node:test';
import {readDevelopmentSession,clearDevelopmentSession} from '../../src/lib/developmentSession';
test('Development session expiry clears stored credentials',()=>{
 const old=globalThis.window;const storage=new Map<string,string>();
 Object.defineProperty(globalThis,'window',{configurable:true,value:{sessionStorage:{getItem:(k:string)=>storage.get(k)??null,setItem:(k:string,v:string)=>storage.set(k,v),removeItem:(k:string)=>storage.delete(k)},dispatchEvent:()=>true}});
 try{storage.set('slg.localDevelopmentSession',JSON.stringify({accessToken:'TEST-only',expiresAtUtc:new Date(Date.now()-1000).toISOString()}));assert.equal(readDevelopmentSession(),null);assert.equal(storage.size,0);
 storage.set('slg.localDevelopmentSession',JSON.stringify({accessToken:'TEST-only',expiresAtUtc:new Date(Date.now()+60000).toISOString()}));assert.equal(readDevelopmentSession()?.accessToken,'TEST-only');clearDevelopmentSession();assert.equal(readDevelopmentSession(),null);
 }finally{Object.defineProperty(globalThis,'window',{configurable:true,value:old});}
});
