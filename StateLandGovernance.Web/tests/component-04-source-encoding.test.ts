import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,readdirSync} from "node:fs";
import {join} from "node:path";
test("C4 source/assets are valid UTF-8, including the legacy investigation fallback",()=>{
 const decoder=new TextDecoder("utf-8",{fatal:true});
 function check(dir:string){for(const item of readdirSync(dir,{withFileTypes:true})){const path=join(dir,item.name);if(item.isDirectory())check(path);else if(/\.(tsx?|css|json)$/.test(path))assert.doesNotThrow(()=>decoder.decode(readFileSync(path)),path);}}
 check("src/modules/component-04");
});
