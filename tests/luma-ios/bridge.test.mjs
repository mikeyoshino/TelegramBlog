import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import vm from 'node:vm';
test('bundled iOS bridge delivers raw requests and native replies without a generated framework asset',async()=>{
 const source=await readFile(new URL('../../src/TeleGramBlog.iOS/Resources/Raw/wwwroot/native-bridge.js',import.meta.url),'utf8').catch(()=> '');
 const sent=[],received=[];
 const window={webkit:{messageHandlers:{webwindowinterop:{postMessage:m=>sent.push(m)}}},dispatchEvent:e=>received.push(e)};
 vm.runInNewContext(source,{window,CustomEvent:class {constructor(type,options){this.type=type;this.detail=options.detail}}});
 assert.equal(typeof window.HybridWebView?.SendRawMessage,'function');
 window.HybridWebView.SendRawMessage('{"action":"start"}');
 assert.deepEqual(sent,['__RawMessage|{"action":"start"}']);
 window.external.receiveMessage('{"id":"1","result":true}');
 assert.equal(received[0].type,'HybridWebViewMessageReceived');
 assert.equal(received[0].detail.message,'{"id":"1","result":true}');
});
