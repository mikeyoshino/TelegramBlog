import test from 'node:test';
import assert from 'node:assert/strict';
const model = await import('../../src/TeleGramBlog.iOS/Resources/Raw/wwwroot/model.mjs').catch(() => ({}));
const posts = [
 {id:'1:2',group:'1',topic:'1',title:'ทะเล',date:'2026-10-27T10:00:00Z',media:[{kind:'image'},{kind:'video'}]},
 {id:'2:2',group:'2',topic:'1',title:'Coffee',date:'2026-10-26T10:00:00Z',media:[{kind:'image'}]},
 {id:'2:3',group:'2',topic:'1',title:'Film',date:'2026-10-25T10:00:00Z',media:[{kind:'video'}]},
];
test('filter combines group, search and album media types',()=>{
 assert.equal(typeof model.filterPosts,'function');
 assert.deepEqual(model.filterPosts(posts,{group:'1',query:'ทะเล',type:'video'}).map(p=>p.id),['1:2']);
 assert.deepEqual(model.filterPosts(posts,{type:'album'}).map(p=>p.id),['1:2']);
 assert.deepEqual(model.filterPosts(posts,{query:' COFFEE '}).map(p=>p.id),['2:2']);
});
test('favorites span groups and never confuse duplicate message IDs',()=>{
 assert.equal(typeof model.filterPosts,'function');
 assert.deepEqual(model.filterPosts(posts,{tab:'saved',group:'1',saved:new Set(['2:2'])}).map(p=>p.id),['2:2']);
});
test('sorting does not mutate source and invalid filters return empty',()=>{
 assert.equal(typeof model.filterPosts,'function');
 assert.deepEqual(model.filterPosts(posts,{sort:'old'}).map(p=>p.id),['2:3','2:2','1:2']);
 assert.equal(posts[0].id,'1:2');
 assert.deepEqual(model.filterPosts(posts,{group:'missing'}),[]);
});
test('late requests cannot commit after navigation or account reset',()=>{
 assert.equal(typeof model.RequestEpoch,'function');
 const epoch=new model.RequestEpoch();const first=epoch.next();const second=epoch.next();
 assert.equal(epoch.current(first),false);assert.equal(epoch.current(second),true);
 epoch.next();assert.equal(epoch.current(second),false);
});
test('media grid accepts Telegram photo and excludes link-only posts before newest-first ordering',()=>{
 const entries=[
  {id:'1:8',date:'2026-10-27T10:00:00Z',media:[{kind:'photo'}]},
  {id:'1:12',date:'2026-10-28T10:00:00Z',media:[],links:['https://example.com']},
  {id:'1:10',date:'2026-10-27T10:00:00Z',media:[{kind:'video'}]},
  {id:'1:11',date:'2026-10-28T09:00:00Z',media:[{kind:'image'}]}
 ];
 assert.deepEqual(model.filterPosts(entries,{mediaOnly:true}).map(p=>p.id),['1:11','1:10','1:8']);
 assert.deepEqual(model.filterPosts(entries,{type:'photo'}).map(p=>p.id),['1:11','1:8']);
});
