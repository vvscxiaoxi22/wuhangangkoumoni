'use client';

import { useMemo, useState, type ReactNode } from 'react';

type ViewKey =
  | 'dashboard'
  | 'tasks'
  | 'tos'
  | 'assessment'
  | 'monitor'
  | 'faults'
  | 'records'
  | 'diagnostics';

const navItems: { id: ViewKey; label: string; mark: string }[] = [
  { id: 'dashboard', label: '教学工作台', mark: '01' },
  { id: 'tasks', label: '任务发布', mark: '02' },
  { id: 'tos', label: 'TOS 任务收件箱', mark: '03' },
  { id: 'assessment', label: '考核中心', mark: '04' },
  { id: 'monitor', label: '实时监控', mark: '05' },
  { id: 'faults', label: '故障设置', mark: '06' },
  { id: 'records', label: '成绩档案', mark: '07' },
  { id: 'diagnostics', label: '设备诊断', mark: '08' },
];

const viewCopy: Record<Exclude<ViewKey, 'dashboard'>, { eyebrow: string; title: string; description: string }> = {
  tasks: { eyebrow: 'TASK BUILDER', title: '任务发布', description: '从教学模板或已校验的业务任务生成任务实例，并下达到指定学生与工位。' },
  tos: { eyebrow: 'TOS INBOX', title: 'TOS 任务收件箱', description: '拉取装卸箱作业指令，完成场景映射、字段校验和教师审核后进入任务队列。' },
  assessment: { eyebrow: 'ASSESSMENT', title: '考核中心', description: '锁定任务、评分规则与故障配置；支持开始、暂停、结束和异常处置。' },
  monitor: { eyebrow: 'LIVE MONITOR', title: '实时监控', description: '查看各工位任务进度、PLC/Gateway 健康度、报警与学生当前得分。' },
  faults: { eyebrow: 'FAULT LAB', title: '故障设置', description: '在已发布题库中选择故障范围、提示策略和多故障组合，不直接修改 PLC 地址。' },
  records: { eyebrow: 'RECORDS', title: '成绩档案', description: '按学生、班级、机型和任务查询成绩，追溯扣分事件、异常和任务版本。' },
  diagnostics: { eyebrow: 'STATION HEALTH', title: '设备诊断', description: '集中查看 Gateway、PLC、急停、控制合断、手柄回中和版本一致性。' },
};

const stations = [
  { id: 'QC-01', name: '岸桥实训工位', asset: '65 吨岸桥 · 就地操作', student: '张宇航', status: '执行中', task: '卸船任务 · V02 / 07 Bay', score: '92', health: 'PLC / Gateway 在线', tone: 'blue' },
  { id: 'YC-01', name: '场桥实训工位', asset: '40.5 吨场桥 · 就地操作', student: '李思涵', status: '准备检查', task: '收箱任务 · Q01-44-E-5', score: '--', health: 'PLC / Gateway 在线', tone: 'amber' },
];

const tosTasks = [
  { id: 'TOS-20260824-081', type: '卸船', route: 'V02-07-56 → QC-01 / AGV-L03', target: '岸桥 QC-01', state: '映射通过' },
  { id: 'TOS-20260824-093', type: '收箱', route: '陆侧外集卡 L02 → Q01-44-E-5', target: '场桥 YC-01', state: '待教师审核' },
  { id: 'TOS-20260824-097', type: '装船', route: 'Q01-34-C-3 → AGV-S02 → V02-09-42', target: '岸桥 / 场桥链路', state: '字段待补齐' },
];

const taskTemplates = [
  { title: '岸桥卸船', subtitle: '船舶贝位 → AGV 作业车道', tag: 'QC-W02', color: 'blue' },
  { title: '场桥收箱', subtitle: '陆侧外集卡 → 堆场箱位', tag: 'YC-W02', color: 'amber' },
  { title: '综合转运', subtitle: '岸桥 / AGV / 场桥联动作业', tag: 'CHAIN-01', color: 'ink' },
];

function Badge({ children, tone = 'muted' }: { children: ReactNode; tone?: 'green' | 'blue' | 'amber' | 'red' | 'muted' }) {
  return <span className={`badge badge-${tone}`}>{children}</span>;
}

function StationCard({ station, onOpen }: { station: (typeof stations)[number]; onOpen: () => void }) {
  return (
    <article className="station-card">
      <div className="station-topline">
        <div><p className="station-id">{station.id}</p><h3>{station.name}</h3></div>
        <Badge tone={station.status === '执行中' ? 'green' : 'amber'}>{station.status}</Badge>
      </div>
      <p className="station-asset">{station.asset}</p>
      <div className={`station-visual ${station.tone}`}>
        <div className="crane-line line-a" /><div className="crane-line line-b" />
        <div className="container container-a" /><div className="container container-b" />
        <span>{station.tone === 'blue' ? '岸桥作业视景' : '堆场作业视景'}</span>
      </div>
      <dl className="station-details">
        <div><dt>当前学生</dt><dd>{station.student}</dd></div>
        <div><dt>当前任务</dt><dd>{station.task}</dd></div>
        <div><dt>设备状态</dt><dd className="online-dot">{station.health}</dd></div>
      </dl>
      <div className="station-bottomline"><span>实时得分 <strong>{station.score}</strong></span><button className="text-button" onClick={onOpen}>查看工位</button></div>
    </article>
  );
}

export default function Home() {
  const [active, setActive] = useState<ViewKey>('dashboard');
  const [deployment, setDeployment] = useState<'standalone' | 'classroom'>('classroom');
  const [showPublisher, setShowPublisher] = useState(false);
  const [source, setSource] = useState<'manual' | 'tos'>('manual');
  const [notice, setNotice] = useState('');
  const activeLabel = useMemo(() => navItems.find((item) => item.id === active)?.label ?? '', [active]);

  const toast = (message: string) => { setNotice(message); window.setTimeout(() => setNotice(''), 2800); };
  const openPublisher = (nextSource: 'manual' | 'tos' = 'manual') => { setSource(nextSource); setShowPublisher(true); };

  return (
    <main className="app-shell">
      <aside className="sidebar">
        <div className="brand-block"><div className="brand-symbol"><span>港</span></div><div><p className="brand-eyebrow">PORT TRAINING</p><h1>教师控制台</h1></div></div>
        <nav aria-label="教师端主菜单" className="navigation">
          <p className="nav-caption">教学管理</p>
          {navItems.map((item) => <button key={item.id} className={`nav-item ${active === item.id ? 'active' : ''}`} onClick={() => setActive(item.id)}><span className="nav-mark">{item.mark}</span><span>{item.label}</span></button>)}
        </nav>
        <section className="side-health"><div className="side-health-header"><span className="pulse" /> 服务运行正常</div><p>本地训练服务<br /><strong>{deployment === 'classroom' ? '2 个工位已连接' : '单机模式已启用'}</strong></p></section>
        <div className="teacher-profile"><div className="avatar">林</div><div><strong>林老师</strong><span>港机实训教师</span></div><button aria-label="账户菜单" className="more-button">•••</button></div>
      </aside>

      <section className="workspace">
        <header className="topbar">
          <div className="crumb"><span>港口实训平台</span><i>/</i><strong>{activeLabel}</strong></div>
          <div className="top-actions">
            <div className="deployment-switch" aria-label="部署模式"><button className={deployment === 'standalone' ? 'selected' : ''} onClick={() => setDeployment('standalone')}>单机教学</button><button className={deployment === 'classroom' ? 'selected' : ''} onClick={() => setDeployment('classroom')}>教室多机</button></div>
            <span className="date-chip">2026.08.24 · 周一</span><button className="primary-button" onClick={() => openPublisher()}>发布任务 <span>+</span></button>
          </div>
        </header>
        {active === 'dashboard' ? <Dashboard deployment={deployment} onOpenPublisher={openPublisher} onNavigate={setActive} /> : <ModuleView active={active} onPublish={openPublisher} onNavigate={setActive} onToast={toast} />}
      </section>

      {showPublisher && <div className="modal-backdrop" role="presentation" onMouseDown={() => setShowPublisher(false)}>
        <section className="publisher-modal" role="dialog" aria-modal="true" aria-labelledby="publisher-title" onMouseDown={(event) => event.stopPropagation()}>
          <div className="modal-header"><div><p className="modal-eyebrow">NEW ASSIGNMENT</p><h2 id="publisher-title">发布训练任务</h2></div><button className="close-button" onClick={() => setShowPublisher(false)} aria-label="关闭">×</button></div>
          <div className="source-tabs"><button className={source === 'manual' ? 'tab-active' : ''} onClick={() => setSource('manual')}>教学模板</button><button className={source === 'tos' ? 'tab-active' : ''} onClick={() => setSource('tos')}>TOS 任务生成</button></div>
          {source === 'manual' ? <div className="template-grid modal-template-grid">{taskTemplates.map((template) => <button className="template-card" key={template.tag} onClick={() => toast(`已选择 ${template.title} 模板`)}><span className={`template-dot ${template.color}`} /><strong>{template.title}</strong><small>{template.subtitle}</small><em>{template.tag}</em></button>)}</div> : <div className="tos-picker">{tosTasks.slice(0, 2).map((task) => <button key={task.id} onClick={() => toast(`已载入 ${task.id} 的场景映射`)}><span><strong>{task.type}任务</strong><small>{task.id}</small></span><em>{task.route}</em><Badge tone="blue">{task.state}</Badge></button>)}</div>}
          <div className="publisher-fields"><label>下达工位<select defaultValue="QC-01"><option>QC-01 · 岸桥实训工位</option><option>YC-01 · 场桥实训工位</option></select></label><label>执行方式<select defaultValue="practice"><option value="practice">实训模式 · 显示操作提示</option><option value="exam">考核模式 · 锁定评分版本</option></select></label></div>
          <div className="modal-actions"><button className="secondary-button" onClick={() => setShowPublisher(false)}>保存草稿</button><button className="primary-button" onClick={() => { setShowPublisher(false); toast('任务已下达至 QC-01，等待学生端确认'); }}>确认下达</button></div>
        </section>
      </div>}
      {notice && <div className="toast" role="status">{notice}</div>}
    </main>
  );
}

function Dashboard({ deployment, onOpenPublisher, onNavigate }: { deployment: 'standalone' | 'classroom'; onOpenPublisher: (source?: 'manual' | 'tos') => void; onNavigate: (view: ViewKey) => void }) {
  return <div className="content dashboard-content">
    <section className="hero-row"><div><p className="eyebrow">TEACHING OVERVIEW</p><h2>今天的实训，<br /><span>正在港区发生。</span></h2><p className="hero-copy">统一发布任务、查看工位状态、处理异常，并让每一次真实操作都留下可追溯的教学记录。</p></div><div className="hero-actions"><button className="primary-button large" onClick={() => onOpenPublisher()}>新建训练任务 <span>+</span></button><button className="outline-button" onClick={() => onNavigate('tos')}>查看 TOS 收件箱</button><p><span className="pulse" /> {deployment === 'classroom' ? '教师主机正在管理 2 个实训工位' : '教师端与学生端运行在本机'}</p></div></section>
    <section className="metrics-grid" aria-label="教学数据概览"><article><span>已连接工位</span><strong>02</strong><small><i className="dot-green" /> 全部在线</small></article><article><span>今日执行任务</span><strong>06</strong><small>已完成 3 项</small></article><article><span>待教师处理</span><strong>03</strong><small><i className="dot-amber" /> TOS 映射与异常</small></article><article><span>平均实训成绩</span><strong>88.6</strong><small>较上周 <b>+4.2</b></small></article></section>
    <section className="section-heading"><div><p className="eyebrow">LIVE STATIONS</p><h3>实训工位</h3></div><button className="text-button" onClick={() => onNavigate('monitor')}>查看全部监控 →</button></section>
    <section className="stations-grid">{stations.map((station) => <StationCard key={station.id} station={station} onOpen={() => onNavigate('monitor')} />)}</section>
    <section className="lower-grid"><article className="timeline-card"><div className="card-title"><div><p className="eyebrow">TODAY&apos;S FLOW</p><h3>任务动态</h3></div><button className="text-button" onClick={() => onNavigate('assessment')}>考核中心 →</button></div><div className="timeline"><div><time>09:42</time><span className="timeline-dot blue" /><p><strong>张宇航</strong> 已进入岸桥卸船任务 <em>QC-W02</em></p></div><div><time>09:31</time><span className="timeline-dot amber" /><p><strong>TOSPublisher</strong> 生成场桥收箱任务，等待审核</p></div><div><time>09:18</time><span className="timeline-dot green" /><p><strong>李思涵</strong> 完成场桥定位专项，成绩 <em>90 分</em></p></div><div><time>09:03</time><span className="timeline-dot red" /><p><strong>YC-01</strong> 手柄回中检查完成，异常已解除</p></div></div></article>
      <article className="attention-card"><p className="eyebrow">ATTENTION</p><h3>待处理事项</h3><button onClick={() => onNavigate('tos')}><span className="attention-index">01</span><span><strong>TOS 装船任务字段待补齐</strong><small>TOS-20260824-097 · 缺少 AGV 目标车道</small></span><i>→</i></button><button onClick={() => onNavigate('faults')}><span className="attention-index">02</span><span><strong>场桥排故题组待发布</strong><small>YC-Fault-Demo · 4 个故障点</small></span><i>→</i></button><button onClick={() => onNavigate('diagnostics')}><span className="attention-index">03</span><span><strong>岸桥点表版本需复核</strong><small>QC-IO-001 · Gateway 已检测到更新</small></span><i>→</i></button></article></section>
  </div>;
}

function ModuleView({ active, onPublish, onNavigate, onToast }: { active: Exclude<ViewKey, 'dashboard'>; onPublish: (source?: 'manual' | 'tos') => void; onNavigate: (view: ViewKey) => void; onToast: (message: string) => void }) {
  const copy = viewCopy[active];
  return <div className="content module-content"><section className="module-intro"><p className="eyebrow">{copy.eyebrow}</p><h2>{copy.title}</h2><p>{copy.description}</p></section>
    {active === 'tos' ? <section className="list-panel"><div className="panel-toolbar"><div><strong>今日拉取 3 条</strong><span>最近同步：09:31</span></div><button className="primary-button" onClick={() => onPublish('tos')}>从 TOS 生成任务</button></div>{tosTasks.map((task) => <div className="tos-row" key={task.id}><div><Badge tone={task.state === '映射通过' ? 'green' : task.state === '待教师审核' ? 'blue' : 'amber'}>{task.state}</Badge><strong>{task.type}作业 · {task.id}</strong><span>{task.route}</span></div><div><small>{task.target}</small><button className="text-button" onClick={() => onPublish('tos')}>审核并发布 →</button></div></div>)}</section> : active === 'monitor' || active === 'diagnostics' ? <section className="stations-grid monitor-grid">{stations.map((station) => <StationCard key={station.id} station={station} onOpen={() => onToast(`${station.id} 实时视景已打开`)} />)}</section> : active === 'tasks' ? <section className="template-grid large-grid">{taskTemplates.map((template) => <button className="template-card" key={template.tag} onClick={() => onPublish()}><span className={`template-dot ${template.color}`} /><strong>{template.title}</strong><small>{template.subtitle}</small><em>{template.tag}</em><b>创建任务 →</b></button>)}</section> : <section className="placeholder-panel"><div className="placeholder-number">{navItems.find((item) => item.id === active)?.mark}</div><div><h3>{copy.title}功能已纳入一期原型</h3><p>该模块将与统一任务中心、学生端状态和本地训练服务连接。当前可从工作台继续演示发布、监控和异常处置主流程。</p><button className="outline-button" onClick={() => onNavigate('dashboard')}>返回教学工作台</button></div></section>}
  </div>;
}
