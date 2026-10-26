import {createRequire} from 'node:module';
import {readFile,mkdir,writeFile} from 'node:fs/promises';
import assert from 'node:assert/strict';
const require=createRequire(import.meta.url);const ts=require('typescript');
const source=await readFile(new URL('../app/prototype-model.ts',import.meta.url),'utf8');
const {outputText}=ts.transpileModule(source,{compilerOptions:{module:ts.ModuleKind.ESNext,target:ts.ScriptTarget.ES2022}});
const m=await import('data:text/javascript;base64,'+Buffer.from(outputText).toString('base64'));
const tests=[];function test(name,fn){fn();tests.push(name);console.log('PASS '+name);}
const qc=m.makeOrder('QC');const twin={...qc,size:'Twin20',containers:m.resizeContainers('Twin20')};
test('new QC order has a valid explicit single-box configuration',()=>assert.deepEqual(m.validateOrder(qc,true),[]));
test('valid Twin20 consists of two ordered FT20 boxes',()=>{assert.deepEqual(m.validateOrder(twin,true),[]);assert.deepEqual(twin.containers.map(b=>b.size),['FT20','FT20']);});
test('duplicate box IDs are rejected ignoring whitespace and case',()=>{const o=structuredClone(twin);o.containers[1].containerId=' '+o.containers[0].containerId.toLowerCase()+' ';assert(m.validateOrder(o,true).some(x=>x.includes('不同箱号')));});
test('Twin20 rejects one, three, null and non-array bindings',()=>{for(const containers of [[twin.containers[0]],[...twin.containers,twin.containers[0]],null,{}])assert(m.validateOrder({...twin,containers},true).length);});
test('Twin20 rejects FT40 container and invalid mass values',()=>{assert(m.validateOrder({...twin,containers:[{...twin.containers[0],size:'FT40'},twin.containers[1]]},true).length);for(const mass of [0,-1,1.2,NaN,Infinity,Number.MAX_SAFE_INTEGER+1])assert(m.validateOrder({...twin,containers:[{...twin.containers[0],grossMassKg:mass},twin.containers[1]]},true).length);});
test('single-box size changes preserve first identity and mass',()=>{const boxes=m.resizeContainers('FT40',twin.containers);assert.equal(boxes.length,1);assert.equal(boxes[0].size,'FT40');assert.equal(boxes[0].containerId,twin.containers[0].containerId);assert.equal(boxes[0].grossMassKg,twin.containers[0].grossMassKg);});
test('five guide plate enum values accepted; unknown and inherited keys denied',()=>{for(const guidePlateMode of Object.keys(m.guidePlateLabels))assert.deepEqual(m.validateOrder({...qc,guidePlateMode},true),[]);for(const guidePlateMode of ['Open','Close','toString','__proto__',''])assert(m.validateOrder({...qc,guidePlateMode},true).length);});
test('YC rejects Twin20 and QC-only cargo/guide fields',()=>{const yc=m.makeOrder('YC');assert.deepEqual(m.validateOrder(yc,true),[]);assert(m.validateOrder({...yc,size:'Twin20'},true).length);assert(m.validateOrder({...yc,guidePlateMode:'AllUp'},true).length);assert(m.validateOrder({...yc,containers:qc.containers},true).length);});
test('legacy QC can be read but cannot start before explicit completion',()=>{const old=structuredClone(qc);delete old.containers;delete old.guidePlateMode;assert.deepEqual(m.validateOrder(old),[]);assert(m.validateOrder(old,true).some(x=>x.includes('旧岸桥工单')));const raw=JSON.stringify({...m.emptyWorkspace(),orders:[old],records:[{id:'legacy-run',student:'demo',events:[],elapsed:1,order:old}]});let stored=raw;globalThis.localStorage={getItem:()=>stored,setItem:(k,v)=>stored=v};const w=m.readWorkspace();assert.equal(w.orders[0].containers,undefined);assert.equal(w.records[0].order.guidePlateMode,undefined);assert.equal(stored,raw);});
test('cargo configuration is isolated in cloned run snapshots',()=>{const original=structuredClone(twin);const snapshot=structuredClone(original);original.containers[0].containerId='CHANGED';original.guidePlateMode='AllDown';assert.equal(snapshot.containers[0].containerId,twin.containers[0].containerId);assert.equal(snapshot.guidePlateMode,twin.guidePlateMode);});
test('default QC load and YC delivery reverse the matching unload/receive locations',()=>{for(const machine of ['QC','YC']){const ts=m.catalogue(machine).filter(t=>t.category==='作业练习');const first=m.makeOrder(machine,ts[0]);const second=m.makeOrder(machine,ts[1]);assert.equal(first.source,second.destination);assert.equal(first.destination,second.source);}});
await mkdir('./outputs/prototype-verification',{recursive:true});
await writeFile('./outputs/prototype-verification/cargo-model-results.json',JSON.stringify({at:new Date().toISOString(),tests},null,2));

